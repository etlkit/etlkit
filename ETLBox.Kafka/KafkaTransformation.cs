using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using ALE.ETLBox.Common.DataFlow;
using Confluent.Kafka;
using DotLiquid;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace ALE.ETLBox.DataFlow
{
    /// <summary>
    /// Transformation sends text messages to Kafka and provides to output rows, successfully processed.
    /// Message template is defined in configuration with <a href="https://shopify.github.io/liquid/">Liquid</a> syntax.
    /// </summary>
    /// <typeparam name="TInput">Parameters for text message template</typeparam>
    /// <typeparam name="TKafkaValue">Kafka value type</typeparam>
    [PublicAPI]
    public abstract class KafkaTransformation<TInput, TKafkaValue>
        : RowTransformation<TInput, TInput?>
    {
        /// <summary>
        /// Kafka topic name
        /// </summary>
        public string TopicName { get; set; } = string.Empty;

        /// <summary>
        /// Kafka producer configuration
        /// </summary>
        public ProducerConfig ProducerConfig { get; set; } = new();

        /// <summary>
        /// Additional configuration for the producer builder, before building producer
        /// </summary>
        public Action<ProducerBuilder<string, TKafkaValue>>? ConfigureProducerBuilder { get; set; }

        /// <summary>
        /// Producer instance override for use in tests
        /// </summary>
        private IProducer<string, TKafkaValue>? _producer;

        /// <summary>
        /// Build Kafka message
        /// </summary>
        protected abstract TKafkaValue BuildMessageValue(TInput input);

        /// <summary>
        /// Build Kafka message key. Returns null by default, so the message is produced without a key
        /// (default partitioning) - preserving backward compatibility for transformations that do not
        /// define a key.
        /// </summary>
        protected virtual string? BuildMessageKey(TInput input) => null;

        /// <summary>
        /// Default constructor
        /// </summary>
        protected KafkaTransformation()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected KafkaTransformation(ILogger<KafkaTransformation<TInput, TKafkaValue>>? logger)
            : base(logger)
        {
            TransformationFunc = SendToKafka;
            InitAction = () =>
                _producer ??= new ProducerBuilder<string, TKafkaValue>(ProducerConfig).Build();
        }

        /// <summary>
        /// Constructor with producer, for unit testing only
        /// </summary>
        protected KafkaTransformation(IProducer<string, TKafkaValue> producer)
            : this()
        {
            _producer = producer;
        }

        protected override void CleanUp(Task transformTask)
        {
            try
            {
                _producer?.Flush();
            }
            finally
            {
                _producer?.Dispose();
            }
            base.CleanUp(transformTask);
        }

        private TInput? SendToKafka(TInput input)
        {
            try
            {
                SendToKafkaInternal(input);
                LogProgress();
                return input;
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;

                var errorData = ErrorHandler.ConvertErrorData(input);
                ErrorHandler.Send(e, errorData);
            }
            return default;
        }

        private void SendToKafkaInternal(TInput input)
        {
            var messageValue = BuildMessageValue(input);
            var message = new Message<string, TKafkaValue> { Value = messageValue };
            var messageKey = BuildMessageKey(input);
            // A null key (no key template) leaves Message.Key unset = keyless (default partitioning).
            // A rendered key (including an empty string) is set explicitly and is NOT treated as keyless.
            if (messageKey != null)
            {
                message.Key = messageKey;
            }
            if (_producer == null)
                throw new InvalidOperationException("Producer is not initialized.");
            _producer.Produce(
                TopicName,
                message,
                deliveryReport =>
                {
                    if (deliveryReport.Error.IsError)
                    {
                        Logger.LogError(
                            "Failed: {Message}, Error: {Reason}",
                            deliveryReport.Message.Value,
                            deliveryReport.Error.Reason
                        );
                    }
                }
            );
        }
    }

    public class KafkaStringTransformation<TInput> : KafkaTransformation<TInput, string>
    {
        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public KafkaStringTransformation(ILogger<KafkaStringTransformation<TInput>> logger)
            : base(logger) { }

        /// <summary>
        /// Default constructor
        /// </summary>
        public KafkaStringTransformation() { }

        /// <summary>
        /// Constructor with producer, for unit testing only
        /// </summary>
        protected KafkaStringTransformation(IProducer<string, string> producer)
            : base(producer) { }

        /// <summary>
        /// Message template in <a href="https://shopify.github.io/liquid/">Liquid</a> syntax.
        /// </summary>
        /// <remarks>
        /// Parameters are provided from input source
        /// </remarks>
        public string MessageTemplate { get; set; } = null!;

        /// <summary>
        /// Optional message key template in <a href="https://shopify.github.io/liquid/">Liquid</a> syntax.
        /// </summary>
        /// <remarks>
        /// Parameters are provided from the input source, same mechanism as <see cref="MessageTemplate"/>.
        /// When not set (null or whitespace), messages are produced without a key (default partitioning),
        /// preserving backward compatibility. When the template is set but renders to an empty string,
        /// the message is produced with an explicit empty-string key (an empty key still maps to a
        /// partition and is distinct from a keyless message).
        /// Whether a topic is keyed or keyless is therefore decided once by whether this template is set,
        /// and applies uniformly to every row; the standard implementation never mixes keyed and keyless
        /// messages within a single topic.
        /// </remarks>
        public string? MessageKeyTemplate { get; set; }

        protected override string BuildMessageValue(TInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            return RenderLiquid(input, MessageTemplate);
        }

        protected override string? BuildMessageKey(TInput input)
        {
            if (string.IsNullOrWhiteSpace(MessageKeyTemplate))
            {
                return null;
            }
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            return RenderLiquid(input, MessageKeyTemplate!);
        }

        private static string RenderLiquid(TInput input, string template)
        {
            var parsedTemplate = Template.Parse(template);
            var inputDictionary =
                input as IDictionary<string, object>
                ?? input!
                    .GetType()
                    .GetProperties()
                    .ToDictionary(p => p.Name, p => p.GetValue(input));
            return parsedTemplate.Render(Hash.FromDictionary(inputDictionary));
        }
    }

    public class KafkaTransformation : KafkaStringTransformation<ExpandoObject>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public KafkaTransformation() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public KafkaTransformation(ILogger<KafkaTransformation> logger)
            : base(logger) { }
    }
}
