using System;
using System.Runtime.Serialization;

namespace EtlKit.Common
{
    /// <summary>
    /// The generic ETLBox Exception. See inner exception for more details.
    /// </summary>
    [Serializable]
    public sealed class EtlKitException : Exception
    {
        public EtlKitException() { }

        public EtlKitException(string message)
            : base(message) { }

        public EtlKitException(string message, Exception innerException)
            : base(message, innerException) { }

        private EtlKitException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
