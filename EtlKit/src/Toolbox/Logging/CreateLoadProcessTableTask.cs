using EtlKit.Common.ControlFlow;
using EtlKit.ControlFlow;
using EtlKit.Primitives;

namespace EtlKit.Logging
{
    /// <summary>
    /// Will create the default load process table for the default database logging.
    /// You can then use the `StartLoadProcessTask`, `AbortLoadProcessTask`, `EndLoadProcessTask`.
    /// These will generate entries in this table and populate the right fields.
    /// </summary>
    /// <see cref="StartLoadProcessTask"/>
    /// <see cref="EndLoadProcessTask" />
    /// <see cref="AbortLoadProcessTask" />
    [PublicAPI]
    public sealed class CreateLoadProcessTableTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => "Create default EtlKit load process table";
        public string LoadProcessTableName { get; set; }
        public string Sql => LoadProcessTable.Sql;
        public CreateTableTask LoadProcessTable { get; private set; }

        public void Execute()
        {
            if (DbConnectionManager.ConnectionManagerType == ConnectionManagerType.ClickHouse)
            {
                throw new NotSupportedException(
                    $"'{ConnectionManager.ConnectionManagerType}' is not supported"
                );
            }
            LoadProcessTable.CopyTaskProperties(this);
            LoadProcessTable.DisableLogging = true;
            LoadProcessTable.Create();
            Common.ControlFlow.ControlFlow.LoadProcessTable = LoadProcessTableName;
        }

        public CreateLoadProcessTableTask(string loadProcessTableName)
        {
            LoadProcessTableName = loadProcessTableName;
            InitCreateTableTask();
        }

        public CreateLoadProcessTableTask(
            IConnectionManager connectionManager,
            string loadProcessTableName
        )
            : this(loadProcessTableName)
        {
            ConnectionManager = connectionManager;
        }

        private void InitCreateTableTask()
        {
            List<TableColumn> lpColumns = new List<TableColumn>
            {
                new("id", "BIGINT", allowNulls: false, isPrimaryKey: true, isIdentity: true),
                new("start_date", "DATETIME", allowNulls: false),
                new("end_date", "DATETIME", allowNulls: true),
                new("source", "NVARCHAR(20)", allowNulls: true),
                new("process_name", "NVARCHAR(100)", allowNulls: false) { DefaultValue = "N/A" },
                new("start_message", "NVARCHAR(4000)", allowNulls: true),
                new("is_running", "SMALLINT", allowNulls: false) { DefaultValue = "1" },
                new("end_message", "NVARCHAR(4000)", allowNulls: true),
                new("was_successful", "SMALLINT", allowNulls: false) { DefaultValue = "0" },
                new("abort_message", "NVARCHAR(4000)", allowNulls: true),
                new("was_aborted", "SMALLINT", allowNulls: false) { DefaultValue = "0" },
            };
            LoadProcessTable = new CreateTableTask(LoadProcessTableName, lpColumns)
            {
                DisableLogging = true,
            };
        }

        public static void Create(
            string loadProcessTableName = Common.ControlFlow.ControlFlow.DefaultLoadProcessTableName
        ) => new CreateLoadProcessTableTask(loadProcessTableName).Execute();

        public static void Create(
            IConnectionManager connectionManager,
            string loadProcessTableName = Common.ControlFlow.ControlFlow.DefaultLoadProcessTableName
        ) => new CreateLoadProcessTableTask(connectionManager, loadProcessTableName).Execute();
    }
}
