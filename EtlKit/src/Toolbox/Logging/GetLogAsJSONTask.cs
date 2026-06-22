using System.Linq;

using EtlKit.Primitives;

using EtlKit.Common.ControlFlow;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EtlKit.Logging
{
    /// <summary>
    /// Returns the content of the etl.Log table as a JSON string.
    /// </summary>
    [PublicAPI]
    public class GetLogAsJSONTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => $"Get log as JSON for {LoadProcessKey}";

        public void Execute()
        {
            List<LogEntry> logEntries = ReadLogTableTask.Read(ConnectionManager, LoadProcessKey);
            CalculateEndDate(logEntries);
            LogHierarchyEntry hierarchy = CreateHierarchyStructure(logEntries);
            JSON = JsonConvert.SerializeObject(
                hierarchy,
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                }
            );
        }

        private static void CalculateEndDate(List<LogEntry> logEntries)
        {
            foreach (var startEntry in logEntries.Where(entry => entry.TaskAction == "START"))
            {
                var endEntry = logEntries.Find(
                    entry =>
                        entry.TaskAction == "END"
                        && entry.TaskHash == startEntry.TaskHash
                        && entry.Id > startEntry.Id
                );
                startEntry.EndDate = endEntry?.LogDate;
            }
        }

        private static LogHierarchyEntry CreateHierarchyStructure(List<LogEntry> entries)
        {
            LogHierarchyEntry root = new LogHierarchyEntry(new LogEntry { TaskType = "ROOT" });
            var currentParent = root;
            var currentList = root.Children;
            foreach (LogEntry entry in entries)
            {
                if (
                    ContainerTypes.Contains(entry.TaskType.ToLower()) && entry.TaskAction == "START"
                )
                {
                    var newEntry = new LogHierarchyEntry(entry) { Parent = currentParent };
                    currentList.Add(newEntry);
                    currentParent = newEntry;
                    currentList = newEntry.Children;
                }
                else if (
                    ContainerTypes.Contains(entry.TaskType.ToLower()) && entry.TaskAction == "END"
                )
                {
                    currentParent = currentParent.Parent;
                    currentList = currentParent.Children;
                }
                else if (entry.TaskAction is "START" or "LOG")
                {
                    var hierarchyEntry = new LogHierarchyEntry(entry) { Parent = currentParent };
                    currentList.Add(hierarchyEntry);
                }
            }
            return root;
        }

        /* Public properties */
        public static List<string> ContainerTypes => new() { "sequence", "subpackage", "package" };

        private long? _loadProcessKey;
        public long? LoadProcessKey
        {
            get { return _loadProcessKey ?? Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id; }
            set { _loadProcessKey = value; }
        }

        public List<LogHierarchyEntry> LogEntryHierarchy { get; set; }

        public string JSON { get; private set; }

        public GetLogAsJSONTask Create()
        {
            Execute();
            return this;
        }

        public GetLogAsJSONTask() { }

        public GetLogAsJSONTask(long? loadProcessKey)
            : this()
        {
            LoadProcessKey = loadProcessKey;
        }

        public static string GetJSON() => new GetLogAsJSONTask().Create().JSON;

        public static string GetJSON(long? loadProcessKey) =>
            new GetLogAsJSONTask(loadProcessKey).Create().JSON;

        public static string GetJSON(IConnectionManager connectionManager) =>
            new GetLogAsJSONTask { ConnectionManager = connectionManager }
                .Create()
                .JSON;

        public static string GetJSON(IConnectionManager connectionManager, int? loadProcessKey) =>
            new GetLogAsJSONTask(loadProcessKey) { ConnectionManager = connectionManager }
                .Create()
                .JSON;
    }
}
