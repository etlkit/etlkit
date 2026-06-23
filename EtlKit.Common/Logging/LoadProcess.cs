using System;

namespace EtlKit.Common.Logging
{
    public class LoadProcess
    {
        public long? Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Source { get; set; }
        public string ProcessName { get; set; }
        public string StartMessage { get; set; }
        public bool IsRunning { get; set; }
        public string EndMessage { get; set; }
        public bool WasSuccessful { get; set; }
        public string AbortMessage { get; set; }
        public bool WasAborted { get; set; }
        public bool IsFinished => WasSuccessful || WasAborted;
    }
}
