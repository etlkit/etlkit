using EtlKit.Common.ControlFlow;
using EtlKit.Common.Logging;
using EtlKit.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EtlKit.Logging
{
    /// <summary>
    /// Returns the content of the LoadProcess table as JSON.
    /// The table name is read from `ControlFlow.LoadProcessTable`. The default table name is `etlkit_log`.
    /// </summary>
    [PublicAPI]
    public class GetLoadProcessAsJSONTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => "Get load process list as JSON";

        public void Execute()
        {
            var read = new ReadLoadProcessTableTask
            {
                ReadOption = ReadOptions.ReadAllProcesses,
                ConnectionManager = ConnectionManager,
            };
            read.Execute();
            List<LoadProcess> logEntries = read.AllLoadProcesses;
            JSON = JsonConvert.SerializeObject(
                logEntries,
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                }
            );
        }

        public string JSON { get; private set; }

        public GetLoadProcessAsJSONTask Create()
        {
            Execute();
            return this;
        }

        public static string GetJSON() => new GetLoadProcessAsJSONTask().Create().JSON;

        public static string GetJSON(IConnectionManager connectionManager) =>
            new GetLoadProcessAsJSONTask { ConnectionManager = connectionManager }
                .Create()
                .JSON;
    }
}
