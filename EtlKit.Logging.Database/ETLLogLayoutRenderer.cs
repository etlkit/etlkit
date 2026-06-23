using System.Text;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;

namespace EtlKit.Logging.Database
{
    [LayoutRenderer("etllog")]
    public class ETLLogLayoutRenderer : LayoutRenderer
    {
        [DefaultParameter]
        public string LogType { get; set; } = "message";

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
