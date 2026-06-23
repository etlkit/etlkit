namespace EtlKit.DataFlow.Models
{
    /// <summary>
    /// Container for an exchange name, exchange type and
    /// routing key, usable as the target address of a message to be published.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The syntax used for the external representation of instances
    /// of this class is compatible with QPid's "Reply-To" field
    /// pseudo-URI format. The pseudo-URI format is
    /// (exchange-type)://(exchange-name)/(routing-key), where
    /// exchange-type is one of the permitted exchange type names (see
    /// class ExchangeType), exchange-name must be present but may be
    /// empty, and routing-key must be present but may be empty.
    /// </para>
    /// <para>
    /// The syntax is as it is solely for compatibility with QPid's
    /// existing usage of the ReplyTo field; the AMQP specifications
    /// 0-8 and 0-9 do not define the format of the field, and do not
    /// define any format for the triple (exchange name, exchange
    /// type, routing key) that could be used instead.
    /// </para>
    /// </remarks>
    public sealed class PublicationAddress
    {
        /// <summary>
        /// Retrieve the exchange name.
        /// </summary>
        public string? ExchangeName { get; set; }

        /// <summary>
        /// Retrieve the exchange type string.
        /// </summary>
        public string? ExchangeType { get; set; }

        /// <summary>
        ///Retrieve the routing key.
        /// </summary>
        public string? RoutingKey { get; set; }
    }
}
