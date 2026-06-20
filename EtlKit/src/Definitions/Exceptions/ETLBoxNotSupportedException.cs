using System.Runtime.Serialization;

namespace EtlKit
{
    /// <summary>
    /// The generic ETLBox Exception. See inner exception for more details.
    /// </summary>
    [Serializable]
    public sealed class ETLBoxNotSupportedException : Exception
    {
        public ETLBoxNotSupportedException() { }

        public ETLBoxNotSupportedException(string message)
            : base(message) { }

        public ETLBoxNotSupportedException(string message, Exception innerException)
            : base(message, innerException) { }

        private ETLBoxNotSupportedException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
