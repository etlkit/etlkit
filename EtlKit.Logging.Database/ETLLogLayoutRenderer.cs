using System.Text;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;

namespace EtlKit.Logging.Database
{
    /// <summary>
    /// NLog layout renderer registered as <c>${etllog}</c>. Renders one of the structured properties
    /// EtlKit attaches to its log events (see <c>docs/controlflow/logging.md</c>), selected via
    /// <see cref="LogType"/>. Independent of <see cref="DatabaseLoggingConfiguration"/> — this renderer
    /// is for text-based NLog targets (console, file) configured directly in an NLog config file.
    /// </summary>
    [LayoutRenderer("etllog")]
    public class ETLLogLayoutRenderer : LayoutRenderer
    {
        /// <summary>
        /// Selects which value to render: <c>"message"</c> (default), <c>"type"</c>, <c>"action"</c>,
        /// <c>"hash"</c>, <c>"stage"</c>, or <c>"loadprocesskey"</c>. Matched case-insensitively. Any
        /// other value renders nothing.
        /// </summary>
        [DefaultParameter]
        public string LogType { get; set; } = "message";

        /// <summary>
        /// Writes the value selected by <see cref="LogType"/> for the current log event.
        /// </summary>
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            switch (LogType?.ToLower())
            {
                case "message":
                    builder.Append(logEvent.Message);
                    break;
                case "type" when logEvent?.Parameters?.Length >= 1:
                    builder.Append(logEvent.Parameters[0]);
                    break;
                case "action" when logEvent?.Parameters?.Length >= 2:
                    builder.Append(logEvent.Parameters[1]);
                    break;
                case "hash" when logEvent?.Parameters?.Length >= 3:
                    builder.Append(logEvent.Parameters[2]);
                    break;
                case "stage" when logEvent?.Parameters?.Length >= 4:
                    builder.Append(logEvent.Parameters[3]);
                    break;
                case "loadprocesskey" when logEvent?.Parameters?.Length >= 5:
                    builder.Append(logEvent.Parameters[4]);
                    break;
            }
        }
    }
}
