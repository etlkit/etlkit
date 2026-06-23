using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

using DotLiquid;

using EtlKit.Common.DataFlow;
using EtlKit.DataFlow.Models;

using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Represents a transformation that performs RabbitMQ publishing.
    /// </summary>
    [PublicAPI]
    public class RabbitMqTransformation<TInput, TOutput> : RowTransformation<TInput, TOutput?>
    {
        private readonly IConnectionFactory? _connectionFactory;

        protected Func<TInput, TOutput>? TransformResult;

        /// <summary>
        /// AMQP Uri to be used for connections.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// RabbitMQ queue
        /// </summary>
        public string Queue { get; set; } = string.Empty;

        /// <summary>Common AMQP Basic content-class headers interface,
        /// spanning the union of the functionality offered by versions
        /// 0-8, 0-8qpid, 0-9 and 0-9-1 of AMQP.</summary>
        public RabbitMqProperties? Properties { get; set; }

        /// <summary>
        /// Template for a message to be published to a queue
        /// </summary>
        public string MessageTemplate { get; set; } = null!;

        /// <summary>
        /// .ctor
        /// </summary>
        public RabbitMqTransformation(Func<TInput, TOutput>? transformResultFunc)
        {
            TransformationFunc = Publish;
            TransformResult = transformResultFunc;
        }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RabbitMqTransformation(
            Func<TInput, TOutput>? transformResultFunc,
            ILogger<RabbitMqTransformation<TInput, TOutput>> logger
        )
            : base(logger)
        {
            TransformationFunc = Publish;
            TransformResult = transformResultFunc;
        }

        /// <summary>
        /// .ctor
        /// </summary>
        public RabbitMqTransformation(
            IConnectionFactory connectionFactory,
            Func<TInput, TOutput>? transformResult
        )
            : this(transformResult)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>
        /// Publish to AMPQ queue
        /// </summary>
        public TOutput? Publish(TInput input)
        {
            try
            {
                var result = PublishInternal(input);
                LogProgress();

                return result;
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(input));
            }

            return default;
        }

        private TOutput? PublishInternal(TInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (_connectionFactory is null && string.IsNullOrEmpty(ConnectionString))
            {
                throw new InvalidOperationException("Connection string can't be null or empty");
            }

            var templateMessage = Template.Parse(MessageTemplate);
            var inputDictionary =
                input as IDictionary<string, object>
                ?? input
                    .GetType()
                    .GetProperties()
                    .ToDictionary(p => p.Name, p => p.GetValue(input));
            var messageValue = templateMessage.Render(Hash.FromDictionary(inputDictionary));

            if (string.IsNullOrEmpty(messageValue))
            {
                return TransformResult == null ? default : TransformResult(input);
            }

            var connectionFactory =
                _connectionFactory ?? new ConnectionFactory { Uri = new Uri(ConnectionString) };

            using var connection = connectionFactory.CreateConnection();
            using var channelToPublish = connection.CreateModel();

            channelToPublish.BasicPublish(
                string.Empty,
                Queue,
                GetChannelProperties(channelToPublish),
                Encoding.Default.GetBytes(messageValue)
            );

            return TransformResult == null ? default : TransformResult(input);
        }

        private IBasicProperties? GetChannelProperties(IModel channelToPublish)
        {
            if (Properties is null)
            {
                return null;
            }

            var properties = channelToPublish.CreateBasicProperties();

            UpdateProperties(properties);

            return properties;
        }

        private void UpdateProperties(IBasicProperties properties)
        {
            if (Properties!.AppId != null)
                properties.AppId = Properties.AppId;
            if (Properties.ClusterId != null)
                properties.ClusterId = Properties.ClusterId;
            if (Properties.ContentEncoding != null)
                properties.ContentEncoding = Properties.ContentEncoding;
            if (Properties.ContentType != null)
                properties.ContentType = Properties.ContentType;
            if (Properties.CorrelationId != null)
                properties.CorrelationId = Properties.CorrelationId;
            UpdateDeliveryMode(properties);
            if (Properties.Expiration != null)
                properties.Expiration = Properties.Expiration;
            UpdateHeaders(properties);
            if (Properties.MessageId != null)
                properties.MessageId = Properties.MessageId;
            UpdatePersistent(properties);
            UpdatePriority(properties);
            if (Properties.ReplyTo != null)
                properties.ReplyTo = Properties.ReplyTo;
            UpdateReplyToAddress(properties);
            if (Properties.Type != null)
                properties.Type = Properties.Type;
            UpdateTimestamp(properties);
            if (Properties.UserId != null)
                properties.UserId = Properties.UserId;
        }

        private void UpdatePriority(IBasicProperties properties)
        {
            if (Properties?.Priority != null)
                properties.Priority = Properties.Priority.GetValueOrDefault();
        }

        private void UpdatePersistent(IBasicProperties properties)
        {
            if (Properties?.Persistent != null)
                properties.Persistent = Properties.Persistent.GetValueOrDefault();
        }

        private void UpdateDeliveryMode(IBasicProperties properties)
        {
            if (Properties?.DeliveryMode != null)
                properties.DeliveryMode = Properties.DeliveryMode.GetValueOrDefault();
        }

        private void UpdateHeaders(IBasicProperties properties)
        {
            if (Properties?.Headers != null)
                properties.Headers = Properties.Headers.ToDictionary(
                    h => h.Key,
                    h => (object)h.Value
                );
        }

        private void UpdateTimestamp(IBasicProperties properties)
        {
            if (Properties?.Timestamp != null)
                properties.Timestamp = new AmqpTimestamp(Properties.Timestamp.GetValueOrDefault());
        }

        private void UpdateReplyToAddress(IBasicProperties properties)
        {
            if (Properties?.ReplyToAddress != null)
                properties.ReplyToAddress = new RabbitMQ.Client.PublicationAddress(
                    Properties.ReplyToAddress.ExchangeType,
                    Properties.ReplyToAddress.ExchangeName,
                    Properties.ReplyToAddress.RoutingKey
                );
        }
    }

    /// <summary>
    /// Non-generic RabbitMQ publishing transformation on dynamically typed data.
    /// </summary>
    public class RabbitMqTransformation : RabbitMqTransformation<ExpandoObject, ExpandoObject>
    {
        /// <summary>
        /// .ctor
        /// </summary>
        public RabbitMqTransformation()
            : base(input => input) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RabbitMqTransformation(ILogger<RabbitMqTransformation> logger)
            : base(input => input, logger) { }

        /// <summary>
        /// .ctor
        /// </summary>
        public RabbitMqTransformation(IConnectionFactory connectionFactory)
            : base(connectionFactory, input => input) { }
    }
}
