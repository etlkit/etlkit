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
    /// Transformation sends messages to Kafka and provides to output rows, successfully processed.
    /// The message value is built by <see cref="BuildMessageValue"/>; the optional message key is built
    /// by the <see cref="MessageKeyResolver"/> delegate.
    /// </summary>
    /// <typeparam name="TInput">Parameters for the message templates</typeparam>
    /// <typeparam name="TKafkaKey">Kafka key type (reference type; null key = keyless message)</typeparam>
    /// <typeparam name="TKafkaValue">Kafka value type</typeparam>
    [PublicAPI]
    public abstract class KafkaTransformation<TInput, TKafkaKey, TKafkaValue>
        : RowTransformation<TInput, TInput?>
        where TKafkaKey : class
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
        public Action<
            ProducerBuilder<TKafkaKey, TKafkaValue>
        >? ConfigureProducerBuilder { get; set; }

        /// <summary>
        /// Producer instance override for use in tests
        /// </summary>
        private IProducer<TKafkaKey, TKafkaValue>? _producer;

        /// <summary>
        /// Build Kafka message value.
        /// </summary>
        protected abstract TKafkaValue BuildMessageValue(TInput input);

        /// <summary>
        /// Optional resolver for the Kafka message key. The keyed/keyless decision is made once, not per row:
        /// when this delegate is null, every message is produced without a key (keyless, default partitioning),
        /// preserving backward compatibility; when it is set, it is expected to return a key for every input
        /// row (the topic is keyed). Subclasses wire it from their own configuration - for example
        /// <see cref="KafkaStringTransformation{TInput}.MessageKeyTemplate"/>.
        /// </summary>
        protected Func<TInput, TKafkaKey>? MessageKeyResolver { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        protected KafkaTransformation()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected KafkaTransformation(
            ILogger<KafkaTransformation<TInput, TKafkaKey, TKafkaValue>>? logger
        )
            : base(logger)
        {
            TransformationFunc = SendToKafka;
            InitAction = () =>
                _producer ??= new ProducerBuilder<TKafkaKey, TKafkaValue>(ProducerConfig).Build();
        }

        /// <summary>
        /// Constructor with producer, for unit testing only
        /// </summary>
        protected KafkaTransformation(IProducer<TKafkaKey, TKafkaValue> producer)
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
            var message = new Message<TKafkaKey, TKafkaValue> { Value = BuildMessageValue(input) };
            // MessageKeyResolver null -> Message.Key stays unset = keyless (default partitioning) for the
            // whole topic. MessageKeyResolver set -> a key is produced for every row (the topic is keyed).
            var keyResolver = MessageKeyResolver;
            if (keyResolver != null)
            {
                message.Key = keyResolver(input);
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

    /// <summary>
    /// Backward-compatible base for string-keyed Kafka transformations. Preserves the original
    /// two-type-parameter shape (the key type is fixed to <see cref="string"/>); use the three-parameter
    /// <see cref="KafkaTransformation{TInput, TKafkaKey, TKafkaValue}"/> for non-string keys.
    /// </summary>
    /// <typeparam name="TInput">Parameters for the message templates</typeparam>
    /// <typeparam name="TKafkaValue">Kafka value type</typeparam>
    [PublicAPI]
    public abstract class KafkaTransformation<TInput, TKafkaValue>
        : KafkaTransformation<TInput, string, TKafkaValue>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected KafkaTransformation() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected KafkaTransformation(
            ILogger<KafkaTransformation<TInput, string, TKafkaValue>>? logger
        )
            : base(logger) { }

        /// <summary>
        /// Constructor with producer, for unit testing only
        /// </summary>
        protected KafkaTransformation(IProducer<string, TKafkaValue> producer)
            : base(producer) { }
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

        private string? _messageKeyTemplate;

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
        public string? MessageKeyTemplate
        {
            get => _messageKeyTemplate;
            set
            {
                _messageKeyTemplate = value;
                // Wire the base key resolver once, by configuration: no template -> keyless for all rows,
                // template set -> render a key for every row.
                MessageKeyResolver = string.IsNullOrWhiteSpace(value)
                    ? null
                    : input => RenderLiquid(input, value!);
            }
        }

        protected override string BuildMessageValue(TInput input) =>
            RenderLiquid(input, MessageTemplate);

        private static string RenderLiquid(TInput input, string template)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            var parsedTemplate = Template.Parse(template);
            var inputDictionary =
                input as IDictionary<string, object>
                ?? input
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
