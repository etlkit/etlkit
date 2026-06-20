using EtlKit.Primitives;

using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Will create a database if the database doesn't exists. In MySql, this will create a schema.
    /// </summary>
    /// <example>
    /// <code>
    /// CreateDatabaseTask.Create("DemoDB");
    /// </code>
    /// </example>
    [PublicAPI]
    public class CreateDatabaseTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => $"Create DB {DatabaseName}";

        public void Execute()
        {
            if (!DbConnectionManager.SupportDatabases)
                throw new ETLBoxNotSupportedException("This task is not supported!");

            var doesExist = new IfDatabaseExistsTask(DatabaseName)
            {
                DisableLogging = true,
                ConnectionManager = ConnectionManager
            }.DoesExist;
            if (!doesExist)
                new SqlTask(this, Sql).ExecuteNonQuery();
        }

        public void Create() => Execute();

        /* Public properties */
        public string DatabaseName { get; set; }
        public RecoveryModel RecoveryModel { get; set; } = RecoveryModel.Simple;
        public string Collation { get; set; }
        public string Sql
        {
            get
            {
                if (ConnectionType == ConnectionManagerType.SqlServer)
                {
                    return $@"
USE [master]

CREATE DATABASE {QB}{DatabaseName}{QE} {CollationString} 
{RecoveryString}

--wait for database to enter 'ready' state
DECLARE @dbReady BIT = 0
WHILE (@dbReady = 0)
BEGIN
SELECT @dbReady = CASE WHEN DATABASEPROPERTYEX('{DatabaseName}', 'Collation') IS NULL THEN 0 ELSE 1 END                    
END
";
                }

                if (ConnectionType == ConnectionManagerType.ClickHouse)
                {
                    return $@"CREATE DATABASE {QB}{DatabaseName}{QE}";
                }

                return $@"CREATE DATABASE {QB}{DatabaseName}{QE} {CollationString}";
            }
        }

        /* Some constructors */
        public CreateDatabaseTask() { }

        public CreateDatabaseTask(string databaseName)
            : this()
        {
            DatabaseName = databaseName;
        }

        public CreateDatabaseTask(string databaseName, string collation)
            : this(databaseName)
        {
            Collation = collation;
        }

        /* Static methods for convenience */
        public static void Create(string databaseName) =>
            new CreateDatabaseTask(databaseName).Execute();

        public static void Create(string databaseName, string collation) =>
            new CreateDatabaseTask(databaseName, collation).Execute();

        public static void Create(IConnectionManager connectionManager, string databaseName) =>
            new CreateDatabaseTask(databaseName)
            {
                ConnectionManager = connectionManager
            }.Execute();

        public static void Create(
            IConnectionManager connectionManager,
            string databaseName,
            string collation
        ) =>
            new CreateDatabaseTask(databaseName, collation)
            {
                ConnectionManager = connectionManager
            }.Execute();

        /* Implementation & stuff */
        private string RecoveryModelAsString
        {
            get =>
                RecoveryModel switch
                {
                    RecoveryModel.Simple => "SIMPLE",
                    RecoveryModel.BulkLogged => "BULK",
                    RecoveryModel.Full => "FULL",
                    _ => string.Empty
                };
        }

        private bool HasCollation => !string.IsNullOrWhiteSpace(Collation);

        private string CollationString
        {
            get
            {
                if (!HasCollation)
                    return string.Empty;
                if (ConnectionType == ConnectionManagerType.Postgres)
                    return "LC_COLLATE '" + Collation + "'";
                return "COLLATE " + Collation;
            }
        }

        private string RecoveryString =>
            RecoveryModel != RecoveryModel.Default
                ? $"ALTER DATABASE [{DatabaseName}] SET RECOVERY {RecoveryModelAsString} WITH no_wait"
                : string.Empty;
    }

    public enum RecoveryModel
    {
        Default,
        Simple,
        BulkLogged,
        Full
    }
}
