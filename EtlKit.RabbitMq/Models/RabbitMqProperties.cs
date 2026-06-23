using System.Collections.Generic;

namespace EtlKit.DataFlow.Models
{
    /// <summary>Common AMQP Basic content-class headers interface,
    /// spanning the union of the functionality offered by versions
    /// 0-8, 0-8qpid, 0-9 and 0-9-1 of AMQP.</summary>
    public sealed class RabbitMqProperties
    {
        /// <summary>
        /// Application Id.
        /// </summary>
        public string? AppId { get; set; }

        /// <summary>
        /// Intra-cluster routing identifier (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        public string? ClusterId { get; set; }

        /// <summary>
        /// MIME content encoding.
        /// </summary>
        public string? ContentEncoding { get; set; }

        /// <summary>
        /// MIME content type.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Application correlation identifier.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Non-persistent (1) or persistent (2).
        /// </summary>
        public byte? DeliveryMode { get; set; }

        /// <summary>
        /// Message expiration specification.
        /// </summary>
        public string? Expiration { get; set; }

        /// <summary>
        /// Message header field table.
        /// </summary>
        public Dictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// Application message Id.
        /// </summary>
        public string? MessageId { get; set; }

        /// <summary>
        /// Sets <see cref="DeliveryMode"/> to either persistent (2) or non-persistent (1).
        /// </summary>
        public bool? Persistent { get; set; }

        /// <summary>
        /// Message priority, 0 to 9.
        /// </summary>
        public byte? Priority { get; set; }

        /// <summary>
        /// Destination to reply to.
        /// </summary>
        public string? ReplyTo { get; set; }

        /// <summary>
        /// Convenience property
        /// </summary>
        public PublicationAddress? ReplyToAddress { get; set; }

        /// <summary>
        /// Message timestamp.
        /// </summary>
        public long? Timestamp { get; set; }

        /// <summary>
        /// Message type name.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// User Id.
        /// </summary>
        public string? UserId { get; set; }
    }
}
