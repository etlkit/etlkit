using System.Text;
using System.Xml;
using Npgsql;
using Xunit;

namespace EtlKit.PostgresStreaming.Tests;

/// <summary>
/// Acceptance test for reading a table's tail from a pipeline defined entirely in XML: no compiled
/// mapper, no compiled position selector, and a run that ends by itself so it can be scheduled.
/// </summary>
[Collection("Postgres")]
#pragma warning disable SP3110
public sealed class XmlPackageTailReadTests : IClassFixture<PostgresContainerFixture>
#pragma warning restore SP3110
{
    private const string SourceTable = "xml_tail_source";
    private const string TargetTable = "xml_tail_target";
    private const string CheckpointTable = "xml_tail_checkpoint";
    private const string CheckpointId = "xml-tail-consumer";

    private readonly PostgresContainerFixture _fixture;

    public XmlPackageTailReadTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void XmlPackage_TailsTableInBatches_StopsWhenEmpty_AndCommitsAfterDestination()
    {
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        SetupSchema(conn);
        InsertSource(conn, "alpha");
        InsertSource(conn, "beta");
        InsertSource(conn, "gamma");

        // No cancellation token: StopWhenEmpty is what has to end the run.
        RunPackage();

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, ReadTarget(conn));
        Assert.Equal(3L, ReadCheckpoint(conn));

        // A second scheduled run must resume past the committed position, not replay the tail.
        InsertSource(conn, "delta");
        RunPackage();

        Assert.Equal(new[] { "alpha", "beta", "gamma", "delta" }, ReadTarget(conn));
        Assert.Equal(4L, ReadCheckpoint(conn));

        // Nothing new: the run still terminates, and the checkpoint stays where it was.
        RunPackage();

        Assert.Equal(4, ReadTarget(conn).Count);
        Assert.Equal(4L, ReadCheckpoint(conn));
    }

    [Fact]
    public void XmlPackage_WithoutCheckpointTableRow_StartsFromBeginningOfTable()
    {
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        conn.Open();
        SetupSchema(conn);
        InsertSource(conn, "only");

        RunPackage();

        Assert.Equal(new[] { "only" }, ReadTarget(conn));
    }

    private void RunPackage()
    {
        using var step = new XmlDataFlowStep();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(BuildPackageXml()));
        using var xmlReader = XmlReader.Create(stream);
        step.ReadXml(xmlReader);
        step.Invoke(CancellationToken.None);
    }

    // BatchSize is deliberately smaller than the number of pending rows so the run has to make
    // several polling rounds and then observe an empty one. Placeholders are substituted rather
    // than interpolated: the SQL template carries Liquid braces, which an interpolated string
    // would try to read as holes of its own.
    private string BuildPackageXml() =>
        PackageXmlTemplate
            .Replace("@connection@", _fixture.ConnectionString, StringComparison.Ordinal)
            .Replace("@sourceTable@", SourceTable, StringComparison.Ordinal)
            .Replace("@targetTable@", TargetTable, StringComparison.Ordinal)
            .Replace("@checkpointTable@", CheckpointTable, StringComparison.Ordinal)
            .Replace("@checkpointId@", CheckpointId, StringComparison.Ordinal);

    private const string PackageXmlTemplate = """
        <XmlDataFlowStep>
          <PostgresXminTailSource>
            <ConnectionManager type="PostgresConnectionManager">
              <ConnectionString type="PostgresConnectionString">
                <Value>@connection@</Value>
              </ConnectionString>
            </ConnectionManager>
            <TableName>@sourceTable@</TableName>
            <Schema>public</Schema>
            <OrderByColumns>
              <Column>stream_position</Column>
            </OrderByColumns>
            <BatchSize>2</BatchSize>
            <StopWhenEmpty>true</StopWhenEmpty>
            <CheckpointId>@checkpointId@</CheckpointId>
            <CheckpointStore type="DbCheckpointStore">
              <ConnectionManager type="PostgresConnectionManager">
                <ConnectionString type="PostgresConnectionString">
                  <Value>@connection@</Value>
                </ConnectionString>
              </ConnectionManager>
              <TableName>public.@checkpointTable@</TableName>
              <KeyColumn>checkpoint_id</KeyColumn>
              <PositionColumn>position</PositionColumn>
            </CheckpointStore>
            <LinkTo>
              <SqlCommandTransformation>
                <ConnectionManager type="PostgresConnectionManager">
                  <ConnectionString type="PostgresConnectionString">
                    <Value>@connection@</Value>
                  </ConnectionString>
                </ConnectionManager>
                <SqlTemplate>insert into public.@targetTable@ (stream_position, name) values ({{ stream_position }}, '{{ name }}')</SqlTemplate>
                <LinkTo>
                  <CheckpointWriter>
                    <CheckpointId>@checkpointId@</CheckpointId>
                    <PositionColumn>stream_position</PositionColumn>
                    <CheckpointStore type="DbCheckpointStore">
                      <ConnectionManager type="PostgresConnectionManager">
                        <ConnectionString type="PostgresConnectionString">
                          <Value>@connection@</Value>
                        </ConnectionString>
                      </ConnectionManager>
                      <TableName>public.@checkpointTable@</TableName>
                      <KeyColumn>checkpoint_id</KeyColumn>
                      <PositionColumn>position</PositionColumn>
                    </CheckpointStore>
                  </CheckpointWriter>
                </LinkTo>
              </SqlCommandTransformation>
            </LinkTo>
          </PostgresXminTailSource>
        </XmlDataFlowStep>
        """;

    private static void SetupSchema(NpgsqlConnection conn)
    {
        Execute(
            conn,
            $"""
            DROP TABLE IF EXISTS public.{SourceTable};
            DROP TABLE IF EXISTS public.{TargetTable};
            DROP TABLE IF EXISTS public.{CheckpointTable};
            CREATE TABLE public.{SourceTable} (
                stream_position BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                name            TEXT NOT NULL
            );
            CREATE TABLE public.{TargetTable} (
                stream_position BIGINT PRIMARY KEY,
                name            TEXT NOT NULL
            );
            CREATE TABLE public.{CheckpointTable} (
                checkpoint_id TEXT PRIMARY KEY,
                position      BIGINT NOT NULL
            );
            """
        );
    }

    private static void InsertSource(NpgsqlConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO public.{SourceTable} (name) VALUES (@n)";
        cmd.Parameters.AddWithValue("n", name);
        cmd.ExecuteNonQuery();
    }

    private static List<string> ReadTarget(NpgsqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT name FROM public.{TargetTable} ORDER BY stream_position";
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static long? ReadCheckpoint(NpgsqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT position FROM public.{CheckpointTable} WHERE checkpoint_id = @id";
        cmd.Parameters.AddWithValue("id", CheckpointId);
        return cmd.ExecuteScalar() as long?;
    }

    private static void Execute(NpgsqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
