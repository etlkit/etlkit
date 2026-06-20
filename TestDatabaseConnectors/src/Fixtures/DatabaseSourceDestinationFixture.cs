using EtlKit.TestShared.Helper;

namespace EtlKit.TestDatabaseConnectors.Fixtures;

[UsedImplicitly]
public sealed class DatabaseSourceDestinationFixture : IDisposable
{
    private static int s_counter;
    private readonly List<string> _sqliteDbs = new();

    internal string GetSQLiteDbSuffix()
    {
        var sqLiteDbSuffix = $"DataFlow_{s_counter++}";
        _sqliteDbs.Add(sqLiteDbSuffix);
        DatabaseHelper.RecreateDatabase(
            Config.SQLiteConnection,
            SourceConfigSection,
            sqLiteDbSuffix
        );
        return sqLiteDbSuffix;
    }

    public const string SourceConfigSection = "DataFlowSource";
    public const string DestinationConfigSection = "DataFlowDestination";
    public const string OtherConfigSection = "Other";

    public DatabaseSourceDestinationFixture()
    {
        DatabaseHelper.RecreateDatabase(Config.SqlConnection, SourceConfigSection);
        DatabaseHelper.RecreateDatabase(Config.SqlConnection, DestinationConfigSection);
        DatabaseHelper.RecreateDatabase(Config.MySqlConnection, SourceConfigSection);
        DatabaseHelper.RecreateDatabase(Config.MySqlConnection, DestinationConfigSection);
        DatabaseHelper.RecreateDatabase(Config.PostgresConnection, SourceConfigSection);
        DatabaseHelper.RecreateDatabase(Config.PostgresConnection, DestinationConfigSection);
    }

    public void Dispose()
    {
        DatabaseHelper.DropDatabase(Config.SqlConnection, SourceConfigSection);
        DatabaseHelper.DropDatabase(Config.SqlConnection, DestinationConfigSection);
        DatabaseHelper.DropDatabase(Config.MySqlConnection, SourceConfigSection);
        DatabaseHelper.DropDatabase(Config.MySqlConnection, DestinationConfigSection);
        DatabaseHelper.DropDatabase(Config.PostgresConnection, SourceConfigSection);
        DatabaseHelper.DropDatabase(Config.PostgresConnection, DestinationConfigSection);
        foreach (var db in _sqliteDbs)
        {
            DatabaseHelper.DropDatabase(Config.SQLiteConnection, SourceConfigSection, db);
        }
    }

    public void DisposeSqliteDb(string sqLiteDbSuffix)
    {
        // We delete suffix from the list first, otherwise if drop fails, each and every test will fail on disposing fixture
        _sqliteDbs.Remove(sqLiteDbSuffix);
        DatabaseHelper.DropDatabase(Config.SQLiteConnection, SourceConfigSection, sqLiteDbSuffix);
    }
}
