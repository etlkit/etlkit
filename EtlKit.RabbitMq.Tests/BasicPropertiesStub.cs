using RabbitMQ.Client;

namespace EtlKit.RabbitMq.Tests
{
    internal sealed class BasicPropertiesStub : IBasicProperties
    {
        public ushort ProtocolClassId { get; }
        public string? ProtocolClassName { get; }
        public void ClearAppId()
        {
            throw new NotImplementedException();
        }

        public void ClearClusterId()
        {
            throw new NotImplementedException();
        }

        public void ClearContentEncoding()
        {
            throw new NotImplementedException();
        }

        public void ClearContentType()
        {
            throw new NotImplementedException();
        }

        public void ClearCorrelationId()
        {
            throw new NotImplementedException();
        }

        public void ClearDeliveryMode()
        {
            throw new NotImplementedException();
        }

        public void ClearExpiration()
        {
            throw new NotImplementedException();
        }

        public void ClearHeaders()
        {
            throw new NotImplementedException();
        }

        public void ClearMessageId()
        {
            throw new NotImplementedException();
        }

        public void ClearPriority()
        {
            throw new NotImplementedException();
        }

        public void ClearReplyTo()
        {
            throw new NotImplementedException();
        }

        public void ClearTimestamp()
        {
            throw new NotImplementedException();
        }

        public void ClearType()
        {
            throw new NotImplementedException();
        }

        public void ClearUserId()
        {
            throw new NotImplementedException();
        }

        public bool IsAppIdPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsClusterIdPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsContentEncodingPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsContentTypePresent()
        {
            throw new NotImplementedException();
        }

        public bool IsCorrelationIdPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsDeliveryModePresent()
        {
            throw new NotImplementedException();
        }

        public bool IsExpirationPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsHeadersPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsMessageIdPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsPriorityPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsReplyToPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsTimestampPresent()
        {
            throw new NotImplementedException();
        }

        public bool IsTypePresent()
        {
            throw new NotImplementedException();
        }

        public bool IsUserIdPresent()
        {
            throw new NotImplementedException();
        }

        public string? AppId { get; set; }
        public string? ClusterId { get; set; }
        public string? ContentEncoding { get; set; }
        public string? ContentType { get; set; }
        public string? CorrelationId { get; set; }
        public byte DeliveryMode { get; set; }
        public string? Expiration { get; set; }
        public IDictionary<string, object>? Headers { get; set; }
        public string? MessageId { get; set; }
        public bool Persistent { get; set; }
        public byte Priority { get; set; }
        public string? ReplyTo { get; set; }
        public PublicationAddress? ReplyToAddress { get; set; }
        public AmqpTimestamp Timestamp { get; set; }
        public string? Type { get; set; }
        public string? UserId { get; set; }
    }
}
