using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.ControlFlow;
using EtlKit.ControlFlow;

namespace EtlKit.Logging
{
    /// <summary>
    /// Will set the table entry for current load process to ended.
    /// </summary>
    [PublicAPI]
    public class EndLoadProcessTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => $"End process with key {LoadProcessId}";

        public void Execute()
        {
            new SqlTask(this, Sql)
            {
                DisableLogging = true,
                Parameter = new List<QueryParameter>
                {
                    new("CurrentDate", "DATETIME", DateTime.Now),
                    new("EndMessage", "VARCHAR(100)", EndMessage),
                    new("LoadProcessId", "BIGINT", LoadProcessId)
                }
            }.ExecuteNonQuery();
            var readLoadProcessTableTask = new ReadLoadProcessTableTask(this, LoadProcessId)
            {
                DisableLogging = true
            };
            readLoadProcessTableTask.Execute();
            Common.ControlFlow.ControlFlow.CurrentLoadProcess =
                readLoadProcessTableTask.LoadProcess;
        }

        /* Public properties */
        private long? _loadProcessId;
        public long? LoadProcessId
        {
            get { return _loadProcessId ?? Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id; }
            set { _loadProcessId = value; }
        }
        public string EndMessage { get; set; }

        public string Sql =>
            $@"
 UPDATE {TN.QuotedFullName} 
  SET end_date = @CurrentDate
  , is_running = 0
  , was_successful = 1
  , was_aborted = 0
  , end_message = @EndMessage
  WHERE id = @LoadProcessId
";

        private ObjectNameDescriptor TN =>
            new(Common.ControlFlow.ControlFlow.LoadProcessTable, QB, QE);

        public EndLoadProcessTask() { }

        public EndLoadProcessTask(long? loadProcessId)
            : this()
        {
            LoadProcessId = loadProcessId;
        }

        public EndLoadProcessTask(long? loadProcessId, string endMessage)
            : this(loadProcessId)
        {
            EndMessage = endMessage;
        }

        public EndLoadProcessTask(string endMessage)
            : this(null, endMessage) { }

        public static void End() => new EndLoadProcessTask().Execute();

        public static void End(long? loadProcessId) =>
            new EndLoadProcessTask(loadProcessId).Execute();

        public static void End(long? loadProcessId, string endMessage) =>
            new EndLoadProcessTask(loadProcessId, endMessage).Execute();

        public static void End(string endMessage) =>
            new EndLoadProcessTask(null, endMessage).Execute();

        public static void End(IConnectionManager connectionManager) =>
            new EndLoadProcessTask { ConnectionManager = connectionManager }.Execute();

        public static void End(IConnectionManager connectionManager, long? loadProcessId) =>
            new EndLoadProcessTask(loadProcessId)
            {
                ConnectionManager = connectionManager
            }.Execute();

        public static void End(
            IConnectionManager connectionManager,
            long? loadProcessId,
            string endMessage
        ) =>
            new EndLoadProcessTask(loadProcessId, endMessage)
            {
                ConnectionManager = connectionManager
            }.Execute();

        public static void End(IConnectionManager connectionManager, string endMessage) =>
            new EndLoadProcessTask(null, endMessage)
            {
                ConnectionManager = connectionManager
            }.Execute();
    }
}
