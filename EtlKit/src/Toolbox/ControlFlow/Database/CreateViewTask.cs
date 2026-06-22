using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Creates or updates a view.
    /// </summary>
    [PublicAPI]
    public class CreateViewTask : GenericTask
    {
        public override string TaskName => $"{CreateOrAlterSql} VIEW {ViewName}";

        public void Execute()
        {
            IsExisting = new IfTableOrViewExistsTask(ViewName)
            {
                ConnectionManager = ConnectionManager,
                DisableLogging = true
            }.Exists();
            if (
                ConnectionType
                    is ConnectionManagerType.SQLite
                        or ConnectionManagerType.Postgres
                        or ConnectionManagerType.Access
                        or ConnectionManagerType.ClickHouse
                && IsExisting
            )
                new DropViewTask(ViewName)
                {
                    ConnectionManager = ConnectionManager,
                    DisableLogging = true
                }.Drop();
            new SqlTask(this, Sql).ExecuteNonQuery();
        }

        public string ViewName { get; set; }
        public ObjectNameDescriptor VN => new(ViewName, QB, QE);

        private string CreateViewName =>
            ConnectionType == ConnectionManagerType.Access
                ? VN.UnquotedFullName
                : VN.QuotedFullName;
        public string Definition { get; set; }
        public string Sql
        {
            get
            {
                return $@"{CreateOrAlterSql} VIEW {CreateViewName}
AS
{Definition}
";
            }
        }

        public CreateViewTask() { }

        public CreateViewTask(string viewName, string definition)
            : this()
        {
            ViewName = viewName;
            Definition = definition;
        }

        public static void CreateOrAlter(string viewName, string definition) =>
            new CreateViewTask(viewName, definition).Execute();

        public static void CreateOrAlter(
            IConnectionManager connectionManager,
            string viewName,
            string definition
        ) =>
            new CreateViewTask(viewName, definition)
            {
                ConnectionManager = connectionManager
            }.Execute();

        private bool IsExisting { get; set; }

        private string CreateOrAlterSql =>
            IsExisting
            && ConnectionType != ConnectionManagerType.SQLite
            && ConnectionType != ConnectionManagerType.Postgres
            && ConnectionType != ConnectionManagerType.ClickHouse
                ? "ALTER"
                : "CREATE";
    }
}
