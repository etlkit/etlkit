using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using DotLiquid;
using EtlKit.Common.DataFlow;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
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
        /// Kafka producer configuration.
        /// </summary>
        /// <remarks>
        /// <see cref="Confluent.Kafka.ProducerConfig.MessageTimeoutMs"/> bounds how long
        /// <see cref="SendToKafkaInternal"/> blocks per row while waiting for a delivery report. Because
        /// <c>SendToKafkaInternal</c> sends and waits for one row at a time, librdkafka's own default of
        /// 300000 ms (5 minutes) would let a single unreachable broker stall the pipeline for that long
        /// on the very first row - too slow to be useful as a fail-fast signal. If left unset, the
        /// constructor's <c>InitAction</c> defaults it to 30000 ms (30 seconds) instead; set it
        /// explicitly beforehand (before the transformation starts) to override.
        /// </remarks>
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
            {
                // ETL-specific default: per-row blocking send (see SendToKafkaInternal) needs a fast
                // fail, not librdkafka's 5-minute default.
                ProducerConfig.MessageTimeoutMs ??= 30000;
                _producer ??= new ProducerBuilder<TKafkaKey, TKafkaValue>(ProducerConfig).Build();
            };
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
                // SendToKafkaInternal already blocks on each message's delivery report before the
                // next one is produced, so by the time the block completes nothing is left in-flight
                // for Flush() to wait on - this call is effectively a no-op today, kept as a cheap
                // safety net in case that per-row blocking model ever changes.
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

        /// <summary>
        /// Produces a single message and blocks until its delivery report arrives, so a delivery
        /// failure surfaces as an exception on this thread for <see cref="SendToKafka"/> to route into
        /// the error buffer.
        /// </summary>
        /// <remarks>
        /// <c>Produce()</c> is itself fire-and-forget; the delivery report only arrives asynchronously
        /// on librdkafka's poll thread. Blocking on it here is what makes an unreachable broker fail the
        /// pipeline instead of being silently swallowed by the async callback.
        /// <para>
        /// Trade-off: the transformation's underlying <c>TransformBlock</c> runs with the default
        /// <c>MaxDegreeOfParallelism</c> of 1 (not currently configurable for
        /// <see cref="EtlKit.Common.DataFlow.RowTransformation{TInput,TOutput}"/>), so only one message
        /// is ever in flight - every row pays a full broker round-trip before the next one is produced.
        /// This turns batch producing into synchronous request/response and defeats librdkafka's own
        /// batching/pipelining, in exchange for being able to attribute a delivery failure to the row
        /// that caused it.
        /// </para>
        /// <para>
        /// How long a stuck row can block here is governed by <see cref="ProducerConfig"/>'s
        /// <c>MessageTimeoutMs</c>, which defaults to 30 seconds (see its remarks) rather than
        /// librdkafka's 5-minute default, precisely because of the per-row blocking above.
        /// </para>
        /// </remarks>
        private void SendToKafkaInternal(TInput input)
        {
            var message = new Message<TKafkaKey, TKafkaValue> { Value = BuildMessageValue(input) };
            // MessageKeyResolver null -> Message.Key stays unset = keyless (default partitioning) for the
            // whole topic. MessageKeyResolver set -> a key is produced for every row (the topic is keyed).
            var keyResolver = MessageKeyResolver;
            if (keyResolver != null)
            {
                // Keyed topic: the resolver is expected to return a key for every row. Set Key only when
                // the resolved value is non-null, so the keyless case stays explicit and is never silently
                // mixed in.
                var key = keyResolver(input);
                if (key != null)
                {
                    message.Key = key;
                }
            }
            if (_producer == null)
                throw new InvalidOperationException("Producer is not initialized.");

            var deliveryCompletion = new TaskCompletionSource<
                DeliveryReport<TKafkaKey, TKafkaValue>
            >(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    deliveryCompletion.SetResult(deliveryReport);
                }
            );
            var report = deliveryCompletion.Task.GetAwaiter().GetResult();
            if (report.Error.IsError)
            {
                throw new ProduceException<TKafkaKey, TKafkaValue>(report.Error, report);
            }
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
