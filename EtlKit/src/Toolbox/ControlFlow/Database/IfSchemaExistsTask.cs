using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Checks if a schema exists. In MySql, use the IfDatabaseExistsTask instead.
    /// </summary>
    [PublicAPI]
    public class IfSchemaExistsTask : IfExistsTask
    {
        /* ITask Interface */
        internal override string GetSql()
        {
            if (!DbConnectionManager.SupportSchemas)
                throw new ETLBoxNotSupportedException("This task is not supported!");

            return ConnectionType switch
            {
                ConnectionManagerType.SqlServer
                    => $@"IF EXISTS (SELECT schema_name(schema_id) FROM sys.schemas WHERE schema_name(schema_id) = '{ON.UnquotedObjectName}')
                            SELECT 1
",
                ConnectionManagerType.Postgres
                    => $@"SELECT 1 FROM information_schema.schemata WHERE schema_name = '{ON.UnquotedObjectName}';
",
                _ => string.Empty
            };
        }

        /* Some constructors */
        public IfSchemaExistsTask() { }

        public IfSchemaExistsTask(string schemaName)
            : this()
        {
            ObjectName = schemaName;
        }

        /* Static methods for convenience */
        public static bool IsExisting(string schemaName) =>
            new IfSchemaExistsTask(schemaName).Exists();

        public static bool IsExisting(IConnectionManager connectionManager, string schemaName) =>
            new IfSchemaExistsTask(schemaName) { ConnectionManager = connectionManager }.Exists();
    }
}
