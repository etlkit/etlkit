using EtlKit.Primitives;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Drops a view. Use DropIfExists to drop a view only if it exists.
    /// </summary>
    [PublicAPI]
    public class DropViewTask : DropTask<IfTableOrViewExistsTask>
    {
        internal override string GetSql()
        {
            return ConnectionType switch
            {
                ConnectionManagerType.Access => $"DROP TABLE {ON.QuotedFullName}",
                _ => $"DROP VIEW {ON.QuotedFullName}",
            };
        }

        public DropViewTask() { }

        public DropViewTask(string viewName)
            : this()
        {
            ObjectName = viewName;
        }

        public static void Drop(string viewName) => new DropViewTask(viewName).Drop();

        public static void Drop(IConnectionManager connectionManager, string viewName) =>
            new DropViewTask(viewName) { ConnectionManager = connectionManager }.Drop();

        public static void DropIfExists(string viewName) =>
            new DropViewTask(viewName).DropIfExists();

        public static void DropIfExists(IConnectionManager connectionManager, string viewName) =>
            new DropViewTask(viewName) { ConnectionManager = connectionManager }.DropIfExists();
    }
}
