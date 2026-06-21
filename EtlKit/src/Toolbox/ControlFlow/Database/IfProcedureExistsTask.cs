using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Checks if a procedure exists.
    /// </summary>
    [PublicAPI]
    public class IfProcedureExistsTask : IfExistsTask
    {
        internal override string GetSql()
        {
            if (!DbConnectionManager.SupportProcedures)
                throw new EtlKitNotSupportedException("This task is not supported!");

            return ConnectionType switch
            {
                ConnectionManagerType.SqlServer => $@"
                            IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND object_id = object_id('{ON.QuotedFullName}'))
                                SELECT 1
                            ",
                ConnectionManagerType.MySql => $@"
                             SELECT 1 
                            FROM information_schema.routines 
                            WHERE routine_schema = DATABASE()
                               AND ( routine_name = '{ON.UnquotedFullName}' OR
                                    CONCAT(routine_catalog, '.', routine_name) = '{ON.UnquotedFullName}' )  
                            ",
                ConnectionManagerType.Postgres => $@"
                            SELECT 1
                            FROM pg_catalog.pg_proc
                            JOIN pg_namespace 
                              ON pg_catalog.pg_proc.pronamespace = pg_namespace.oid
                            WHERE ( CONCAT(pg_namespace.nspname,'.',proname) = '{ON.UnquotedFullName}'
                                        OR proname = '{ON.UnquotedFullName}' )
                            ",
                _ => string.Empty,
            };
        }

        public IfProcedureExistsTask() { }

        public IfProcedureExistsTask(string procedureName)
            : this()
        {
            ObjectName = procedureName;
        }

        public static bool IsExisting(string procedureName) =>
            new IfProcedureExistsTask(procedureName).Exists();

        public static bool IsExisting(IConnectionManager connectionManager, string procedureName) =>
            new IfProcedureExistsTask(procedureName)
            {
                ConnectionManager = connectionManager,
            }.Exists();
    }
}
