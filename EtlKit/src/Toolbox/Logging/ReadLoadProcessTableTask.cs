using System.Diagnostics.CodeAnalysis;
using System.Text;

using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.ControlFlow;
using EtlKit.Common.Logging;
using EtlKit.ControlFlow;

namespace EtlKit.Logging
{
    /// <summary>
    /// Read load processes by Id, all processes or last finished/successful/aborted.
    /// </summary>
    [PublicAPI]
    public class ReadLoadProcessTableTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName =>
            "Read load processes by Id, all processes or last finished/successful/aborted.";

        public void Execute()
        {
            LoadProcess = new LoadProcess();
            var sql = new SqlTask(this, Sql)
            {
                DisableLogging = true,
                Actions = new List<Action<object>>
                {
                    col => LoadProcess.Id = Convert.ToInt64(col),
                    col =>
                        LoadProcess.StartDate = col is string str
                            ? DateTime.Parse(
                                str,
                                CultureInfo.CurrentCulture,
                                DateTimeStyles.RoundtripKind
                            )
                            : (DateTime)col,
                    col =>
                        LoadProcess.EndDate = col is string str
                            ? DateTime.Parse(
                                str,
                                CultureInfo.CurrentCulture,
                                DateTimeStyles.RoundtripKind
                            )
                            : (DateTime?)col,
                    col => LoadProcess.Source = (string)col,
                    col => LoadProcess.ProcessName = (string)col,
                    col => LoadProcess.StartMessage = (string)col,
                    col => LoadProcess.IsRunning = Convert.ToInt16(col) > 0,
                    col => LoadProcess.EndMessage = (string)col,
                    col => LoadProcess.WasSuccessful = Convert.ToInt16(col) > 0,
                    col => LoadProcess.AbortMessage = (string)col,
                    col => LoadProcess.WasAborted = Convert.ToInt16(col) > 0,
                },
            };
            if (ReadOption == ReadOptions.ReadAllProcesses)
            {
                sql.BeforeRowReadAction = () => AllLoadProcesses = new List<LoadProcess>();
                sql.AfterRowReadAction = () => AllLoadProcesses.Add(LoadProcess);
            }
            sql.ExecuteReader();
        }

        /* Public properties */
        private long? _loadProcessId;
        public long? LoadProcessId
        {
            get { return _loadProcessId ?? Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id; }
            set { _loadProcessId = value; }
        }
        public LoadProcess LoadProcess { get; private set; }
        public List<LoadProcess> AllLoadProcesses { get; set; }

        [SuppressMessage(
            "Code Quality",
            "S1144:Unused private types or members should be removed",
            Justification = "Private set is reserved for future use."
        )]
        public LoadProcess LastFinished { get; private set; }

        [SuppressMessage(
            "Code Quality",
            "S1144:Unused private types or members should be removed",
            Justification = "Private set is reserved for future use."
        )]
        public LoadProcess LastTransferred { get; private set; }

        public ReadOptions ReadOption { get; set; } = ReadOptions.ReadSingleProcess;

        public string Sql
        {
            get
            {
                StringBuilder sqlBuilder = new();
                sqlBuilder.Append(
                    $@"
SELECT {Top1Sql} {QB}id{QE}, {QB}start_date{QE}, {QB}end_date{QE}, {QB}source{QE}, {QB}process_name{QE}, {QB}start_message{QE}, {QB}is_running{QE}, {QB}end_message{QE}, {QB}was_successful{QE}, {QB}abort_message{QE}, {QB}was_aborted{QE}
FROM {Tn.QuotedFullName}"
                );
                switch (ReadOption)
                {
                    case ReadOptions.ReadSingleProcess:
                        sqlBuilder.Append($"WHERE id = {LoadProcessId.ToString()}");
                        break;
                    case ReadOptions.ReadLastFinishedProcess:
                        sqlBuilder.Append(
                            @"WHERE was_successful = 1 || was_aborted = 1
                              ORDER BY end_date desc, id DESC"
                        );
                        break;
                    case ReadOptions.ReadLastSuccessful:
                        sqlBuilder.Append(
                            @"WHERE was_successful = 1
                              ORDER BY end_date desc, id DESC"
                        );
                        break;
                    case ReadOptions.ReadLastAborted:
                        sqlBuilder.Append(
                            @"WHERE was_aborted = 1
                              ORDER BY end_date desc, id DESC"
                        );
                        break;
                    case ReadOptions.ReadAllProcesses:
                    default:
                        break;
                }

                sqlBuilder.Append(Environment.NewLine);
                sqlBuilder.Append(Limit1Sql);
                return sqlBuilder.ToString();
            }
        }

        private string Top1Sql
        {
            get
            {
                if (ReadOption == ReadOptions.ReadAllProcesses)
                    return string.Empty;
                if (ConnectionType == ConnectionManagerType.SqlServer)
                    return "TOP 1";
                return string.Empty;
            }
        }

        private string Limit1Sql
        {
            get
            {
                if (ReadOption == ReadOptions.ReadAllProcesses)
                    return string.Empty;
                if (ConnectionType == ConnectionManagerType.Postgres)
                    return "LIMIT 1";
                return string.Empty;
            }
        }

        private ObjectNameDescriptor Tn =>
            new(Common.ControlFlow.ControlFlow.LoadProcessTable, QB, QE);

        public ReadLoadProcessTableTask() { }

        public ReadLoadProcessTableTask(long? loadProcessId)
            : this()
        {
            LoadProcessId = loadProcessId;
        }

        public ReadLoadProcessTableTask(ITask callingTask, long? loadProcessId)
            : this(loadProcessId)
        {
            CopyTaskProperties(callingTask);
        }

        public static LoadProcess Read(long? loadProcessId)
        {
            var sql = new ReadLoadProcessTableTask(loadProcessId);
            sql.Execute();
            return sql.LoadProcess;
        }

        public static List<LoadProcess> ReadAll()
        {
            var sql = new ReadLoadProcessTableTask { ReadOption = ReadOptions.ReadAllProcesses };
            sql.Execute();
            return sql.AllLoadProcesses;
        }

        public static LoadProcess ReadWithOption(ReadOptions option)
        {
            var sql = new ReadLoadProcessTableTask { ReadOption = option };
            sql.Execute();
            return sql.LoadProcess;
        }
    }

    public enum ReadOptions
    {
        ReadSingleProcess,
        ReadAllProcesses,
        ReadLastFinishedProcess,
        ReadLastSuccessful,
        ReadLastAborted,
    }
}
