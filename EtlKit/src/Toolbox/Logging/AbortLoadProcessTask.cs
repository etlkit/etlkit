using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.ControlFlow;
using EtlKit.ControlFlow;

namespace EtlKit.Logging
{
    /// <summary>
    /// Will set the table entry for current load process to aborted.
    /// </summary>
    [PublicAPI]
    public sealed class AbortLoadProcessTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => $"Abort process with key {LoadProcessId}";

        public void Execute()
        {
            QueryParameter cd = new QueryParameter("CurrentDate", "DATETIME", DateTime.Now);
            QueryParameter em = new QueryParameter("AbortMessage", "VARCHAR(100)", AbortMessage);
            QueryParameter lpk = new QueryParameter("LoadProcessId", "BIGINT", LoadProcessId);
            new SqlTask(this, Sql)
            {
                DisableLogging = true,
                Parameter = new List<QueryParameter> { cd, em, lpk }
            }.ExecuteNonQuery();
            var tableTask = new ReadLoadProcessTableTask(this, LoadProcessId)
            {
                DisableLogging = true
            };
            tableTask.Execute();
            Common.ControlFlow.ControlFlow.CurrentLoadProcess = tableTask.LoadProcess;
        }

        /* Public properties */
        private long? _loadProcessId;
        public long? LoadProcessId
        {
            get { return _loadProcessId ?? Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id; }
            set { _loadProcessId = value; }
        }
        public string AbortMessage { get; set; }

        public string Sql =>
            $@"
 UPDATE {TN.QuotedFullName} 
  SET end_date = @CurrentDate
  , is_running = 0
  , was_successful = 0
  , was_aborted = 1
  , abort_message = @AbortMessage
  WHERE id = @LoadProcessId
";

        private ObjectNameDescriptor TN =>
            new(Common.ControlFlow.ControlFlow.LoadProcessTable, QB, QE);

        public AbortLoadProcessTask() { }

        public AbortLoadProcessTask(long? loadProcessId)
            : this()
        {
            LoadProcessId = loadProcessId;
        }

        public AbortLoadProcessTask(long? loadProcessId, string abortMessage)
            : this(loadProcessId)
        {
            AbortMessage = abortMessage;
        }

        public AbortLoadProcessTask(string abortMessage)
            : this()
        {
            AbortMessage = abortMessage;
        }

        public static void Abort() => new AbortLoadProcessTask().Execute();

        public static void Abort(long? loadProcessId) =>
            new AbortLoadProcessTask(loadProcessId).Execute();

        public static void Abort(string abortMessage) =>
            new AbortLoadProcessTask(abortMessage).Execute();

        public static void Abort(long? loadProcessId, string abortMessage) =>
            new AbortLoadProcessTask(loadProcessId, abortMessage).Execute();

        public static void Abort(IConnectionManager connectionManager) =>
            new AbortLoadProcessTask { ConnectionManager = connectionManager }.Execute();

        public static void Abort(IConnectionManager connectionManager, long? loadProcessId) =>
            new AbortLoadProcessTask(loadProcessId)
            {
                ConnectionManager = connectionManager
            }.Execute();

        public static void Abort(IConnectionManager connectionManager, string abortMessage) =>
            new AbortLoadProcessTask(abortMessage)
            {
                ConnectionManager = connectionManager
            }.Execute();

        public static void Abort(
            IConnectionManager connectionManager,
            long? loadProcessId,
            string abortMessage
        ) =>
            new AbortLoadProcessTask(loadProcessId, abortMessage)
            {
                ConnectionManager = connectionManager
            }.Execute();
    }
}
