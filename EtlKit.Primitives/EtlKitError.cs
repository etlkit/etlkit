using System;

namespace EtlKit.Primitives
{
    /// <summary>
    /// The generic EtlKit Exception. See the inner exception for more details.
    /// </summary>
    public class EtlKitError
    {
        public string ErrorText { get; set; }
        public DateTime ReportTime { get; set; }
        public Exception Exception { get; set; }
        public string RecordAsJson { get; set; }
    }
}
