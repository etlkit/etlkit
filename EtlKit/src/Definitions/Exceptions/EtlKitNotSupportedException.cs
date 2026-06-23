using System.Runtime.Serialization;

namespace EtlKit
{
    /// <summary>
    /// The generic EtlKit Exception. See inner exception for more details.
    /// </summary>
    [Serializable]
    public sealed class EtlKitNotSupportedException : Exception
    {
        public EtlKitNotSupportedException() { }

        public EtlKitNotSupportedException(string message)
            : base(message) { }

        public EtlKitNotSupportedException(string message, Exception innerException)
            : base(message, innerException) { }

        private EtlKitNotSupportedException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
