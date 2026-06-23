using EtlKit.ControlFlow;
using EtlKit.Primitives;

namespace EtlKit.TestShared.Helper
{
    public static class DatabaseHelper
    {
        private static void DropAndCreate<TConnectionManager>(
            TConnectionManager connManagerMaster,
            string dbName
        )
            where TConnectionManager : IConnectionManager
        {
            DropDatabase(connManagerMaster, dbName);
            CreateDatabase(connManagerMaster, dbName);
        }

        private static void CreateDatabase<TConnectionManager>(
            TConnectionManager connManagerMaster,
            string dbName
        )
            where TConnectionManager : IConnectionManager
        {
            new CreateDatabaseTask(dbName)
            {
                DisableLogging = true,
                ConnectionManager = connManagerMaster,
            }.Execute();
        }

        private static void DropDatabase<TConnectionManager>(
            TConnectionManager connManagerMaster,
            string dbName
        )
            where TConnectionManager : IConnectionManager
        {
            new DropDatabaseTask(dbName)
            {
                DisableLogging = true,
                ConnectionManager = connManagerMaster,
            }.DropIfExists();
        }

        private static void ActOnDatabase<TConnectionManager, TConnectionString>(
            ConnectionDetails<TConnectionString, TConnectionManager> connectionDetails,
            string section,
            string dbNameSuffix,
            Action<TConnectionManager, string> databaseAction
        )
            where TConnectionManager : class, IConnectionManager, new()
            where TConnectionString : IDbConnectionString, new()
        {
            using var connManagerMaster = new TConnectionManager();
            connManagerMaster.ConnectionString = connectionDetails
                .ConnectionString(section)
                .CloneWithMasterDbName();
            var dbName =
                connectionDetails.ConnectionString(section).DbName + (dbNameSuffix ?? string.Empty);
            databaseAction(connManagerMaster, dbName);
        }

        public static void DropDatabase(
            SQLiteConnectionDetails connectionDetails,
            string section,
            string dbNameSuffix = null
        )
        {
            connectionDetails.DeleteDatabase(section, dbNameSuffix);
        }

        public static void DropDatabase<TConnectionManager, TConnectionString>(
            ConnectionDetails<TConnectionString, TConnectionManager> connectionDetails,
            string section,
            string dbNameSuffix = null
        )
            where TConnectionManager : class, IConnectionManager, new()
            where TConnectionString : IDbConnectionString, new() =>
            ActOnDatabase(connectionDetails, section, dbNameSuffix, DropDatabase);

        public static void CreateDatabase<TConnectionManager, TConnectionString>(
            ConnectionDetails<TConnectionString, TConnectionManager> connectionDetails,
            string section,
            string dbNameSuffix = null
        )
            where TConnectionManager : class, IConnectionManager, new()
            where TConnectionString : IDbConnectionString, new() =>
            ActOnDatabase(connectionDetails, section, dbNameSuffix, CreateDatabase);

        public static void RecreateDatabase<TConnectionManager, TConnectionString>(
            ConnectionDetails<TConnectionString, TConnectionManager> connectionDetails,
            string section,
            string dbNameSuffix = null
        )
            where TConnectionManager : class, IConnectionManager, new()
            where TConnectionString : IDbConnectionString, new() =>
            ActOnDatabase(connectionDetails, section, dbNameSuffix, DropAndCreate);

        public static void RecreateDatabase(
            SQLiteConnectionDetails connectionDetails,
            string section,
            string dbNameSuffix = null
        )
        {
            connectionDetails.CopyFromTemplate(section, dbNameSuffix);
        }
    }
}
