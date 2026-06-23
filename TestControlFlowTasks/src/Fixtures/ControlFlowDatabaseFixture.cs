using System.Diagnostics.CodeAnalysis;

namespace EtlKit.TestControlFlowTasks.Fixtures
{
    [CollectionDefinition(nameof(ControlFlowCollection))]
    public class ControlFlowCollection : ICollectionFixture<ControlFlowDatabaseFixture> { }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public sealed class ControlFlowDatabaseFixture : IDisposable
    {
        public const string ConfigSection = "ControlFlow";

        private static int s_counter = 0;

        internal string SQLiteDbSuffix { get; } = $"{ConfigSection}_{s_counter++}";

        public ControlFlowDatabaseFixture()
        {
            DatabaseHelper.RecreateDatabase(Config.SqlConnection, ConfigSection);
            DatabaseHelper.RecreateDatabase(Config.MySqlConnection, ConfigSection);
            DatabaseHelper.RecreateDatabase(Config.PostgresConnection, ConfigSection);
            DatabaseHelper.RecreateDatabase(Config.SQLiteConnection, ConfigSection, SQLiteDbSuffix);
        }

        public void Dispose()
        {
            DatabaseHelper.DropDatabase(Config.SqlConnection, ConfigSection);
            DatabaseHelper.DropDatabase(Config.MySqlConnection, ConfigSection);
            DatabaseHelper.DropDatabase(Config.PostgresConnection, ConfigSection);
            DatabaseHelper.DropDatabase(Config.SQLiteConnection, ConfigSection, SQLiteDbSuffix);
        }
    }
}
