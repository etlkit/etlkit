using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBTransformation;

[Collection(nameof(DataFlowSourceDestinationCollection))]
public class DbRowTransformationTests : DatabaseConnectorsTestBase
{
    public DbRowTransformationTests(DatabaseSourceDestinationFixture fixture)
        : base(fixture) { }

    public class MyExtendedRow
    {
        [ColumnMap("Col1")]
        public int Id { get; set; }

        [ColumnMap("Col3")]
        public long? Value { get; set; }

        [ColumnMap("Col4")]
        public decimal Percentage { get; set; }

        [ColumnMap("Col2")]
        public string Text { get; set; }
    }

    [Theory, MemberData(nameof(AllSqlConnections))]
    public void ColumnMapping(IConnectionManager connection)
    {
        //Arrange
        var source4Columns = new FourColumnsTableFixture(connection, "Source");
        source4Columns.InsertTestData();
        var dest4Columns = new FourColumnsTableFixture(
            connection,
            "Transformation",
            identityColumnIndex: IsIdentitySupported(connection) ? 2 : -1
        );

        var source = new DbSource<string[]>(connection, "Source");
        var trans = new RowTransformation<string[], MyExtendedRow>(row => new MyExtendedRow
        {
            Id = int.Parse(row[0]),
            Text = row[1],
            Value = row[2] != null ? long.Parse(row[2]) : null,
            Percentage = decimal.Parse(row[3]),
        });
        var dbTransformation = new DbRowTransformation<MyExtendedRow>(connection, "Transformation");
        var dest = new MemoryDestination<MyExtendedRow>();

        //Act
        source.LinkTo(trans);
        trans.LinkTo(dbTransformation);
        dbTransformation.LinkTo(dest);
        source.Execute();
        dest.Wait();

        //Assert
        dest4Columns.AssertTestData();
        Assert.Equal(3, dest.Data.Count);
    }
}
