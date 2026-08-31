using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Confluent.Kafka;
using DotLiquid;
using EtlKit.Common.ControlFlow;
using EtlKit.Common.DataFlow;
using EtlKit.Primitives;
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
    /// <remarks>
    /// Internally this is two chained dataflow stages, not one:
    /// <list type="bullet">
    /// <item><description>
    /// a produce stage (<see cref="Produce"/>) that calls <see cref="IProducer{TKey,TValue}.Produce"/>
    /// truly fire-and-forget, so librdkafka can still batch/pipeline on the wire, emitting each row
    /// paired with its pending delivery-report task;
    /// </description></item>
    /// <item><description>
    /// a confirm stage (<see cref="ConfirmAsync"/>) that awaits those pairs strictly in the order the rows
    /// arrived, forwarding a row only once its delivery is confirmed - so a delivery failure is
    /// attributed to the row that caused it and routed to <see cref="ErrorHandler"/> (or thrown) before
    /// any later row is ever forwarded past it.
    /// </description></item>
    /// </list>
    /// This keeps producing fire-and-forget while still failing the pipeline - in row order - on a
    /// delivery error, instead of trading one property off against the other.
    /// <see cref="MaxUnconfirmedMessages"/> bounds how far the produce stage can race ahead of the
    /// confirm stage, so a slow or unreachable broker can't grow the in-flight set without limit.
    /// </remarks>
    [PublicAPI]
    public abstract class KafkaTransformation<TInput, TKafkaKey, TKafkaValue>
        : DataFlowTransformation<TInput, TInput?>,
            IDisposable
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
        /// Passed through to librdkafka as-is. If <see cref="Confluent.Kafka.ProducerConfig.MessageTimeoutMs"/>
        /// is left unset, librdkafka's own default of 300000 ms (5 minutes) applies - this transformation
        /// does not override it. Set it explicitly beforehand (before the transformation starts) if a
        /// different delivery-failure timeout is needed.
        /// </remarks>
        public ProducerConfig ProducerConfig { get; set; } = new();

        /// <summary>
        /// Additional configuration for the producer builder, before building producer
        /// </summary>
        public Action<
            ProducerBuilder<TKafkaKey, TKafkaValue>
        >? ConfigureProducerBuilder { get; set; }

        /// <summary>
        /// Bounds how far the fire-and-forget produce stage can race ahead of the confirm stage, so a
        /// slow or unreachable broker cannot grow the in-flight set without limit. Applied as the
        /// <see cref="ExecutionDataflowBlockOptions.BoundedCapacity"/> of BOTH the produce and confirm
        /// stages the first time this transformation is linked - bounding only one of the two stages does
        /// not provide real backpressure, since a block's BoundedCapacity only gates acceptance of new
        /// input, not its own output buffer - so it must be set before either stage is created.
        /// </summary>
        /// <remarks>
        /// Because both stages are bounded to this same value, the actual steady-state ceiling of
        /// unconfirmed rows is roughly 2x this number, not this number itself: in the handoff window
        /// between the two blocks, a row can transiently count against the produce stage's output buffer
        /// and the confirm stage's input buffer at the same time. Size this value with that in mind
        /// rather than assuming it caps in-flight rows exactly.
        /// </remarks>
        public int MaxUnconfirmedMessages { get; set; } = 1000;

        /// <summary>
        /// Producer instance override for use in tests
        /// </summary>
        // CA2213's dataflow heuristic doesn't recognize disposal through Interlocked.Exchange - the
        // field IS disposed in Dispose(bool) below, just via a captured local rather than a direct
        // "_producer.Dispose(); _producer = null;" the analyzer can pattern-match.
#pragma warning disable CA2213
        private IProducer<TKafkaKey, TKafkaValue>? _producer;
#pragma warning restore CA2213

        private TransformBlock<TInput, ProduceEnvelope>? _produceBlock;
        private TransformBlock<ProduceEnvelope, TInput?>? _confirmBlock;

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

        public override ITargetBlock<TInput> TargetBlock
        {
            get
            {
                EnsureBlocksCreated();
                return _produceBlock!;
            }
        }

        public override ISourceBlock<TInput?> SourceBlock
        {
            get
            {
                EnsureBlocksCreated();
                return _confirmBlock!;
            }
        }

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
            TaskName = "Execute Kafka transformation";
        }

        /// <summary>
        /// Constructor with producer, for unit testing only
        /// </summary>
        protected KafkaTransformation(IProducer<TKafkaKey, TKafkaValue> producer)
            : this()
        {
            _producer = producer;
        }

        /// <summary>
        /// Constructor with producer and an injected logger, for unit testing only.
        /// Injecting the logger keeps the instance off the static
        /// <see cref="ControlFlow.LoggerFactory"/> fallback, so a test observing this
        /// transformation's log records cannot observe those of anything else.
        /// </summary>
        protected KafkaTransformation(
            IProducer<TKafkaKey, TKafkaValue> producer,
            ILogger<KafkaTransformation<TInput, TKafkaKey, TKafkaValue>>? logger
        )
            : this(logger)
        {
            _producer = producer;
        }

        public override void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target) =>
            ErrorHandler.LinkErrorTo(target, SourceBlock.Completion);

        private void EnsureBlocksCreated()
        {
            if (_produceBlock != null && _confirmBlock != null)
                return;

            _produceBlock = new TransformBlock<TInput, ProduceEnvelope>(
                Produce,
                new ExecutionDataflowBlockOptions { BoundedCapacity = MaxUnconfirmedMessages }
            );
            _confirmBlock = new TransformBlock<ProduceEnvelope, TInput?>(
                ConfirmAsync,
                new ExecutionDataflowBlockOptions { BoundedCapacity = MaxUnconfirmedMessages }
            );
            _produceBlock.LinkTo(
                _confirmBlock,
                new DataflowLinkOptions { PropagateCompletion = true }
            );
            // Same idea as RowTransformation's TransformBlockWithCompletion.OnComplete -> CleanUp:
            // release the producer as soon as this transformation's own completion resolves, instead of
            // relying on an external owner to call Dispose().
            _confirmBlock.Completion.ContinueWith(_ => Dispose());
        }

        /// <summary>
        /// Releases the underlying Kafka producer, if one was created. Wired in
        /// <see cref="EnsureBlocksCreated"/> to run automatically once <see cref="SourceBlock"/>'s
        /// <see cref="IDataflowBlock.Completion"/> finishes - the same way
        /// <c>RestTransformation.CleanUp</c> releases its HTTP client as soon as its own transform block
        /// completes, instead of relying on an external owner to call this. Still public and safe to call
        /// again explicitly (idempotent, see <see cref="Dispose(bool)"/>).
        /// </summary>
        /// <remarks>
        /// <see cref="IProducer{TKey,TValue}.Flush(System.Threading.CancellationToken)"/> has nothing left
        /// to wait on when called after completion: the confirm stage already blocks on every row's
        /// delivery report before it advances, so every produced message is already confirmed
        /// (successfully, or routed/thrown as an error) by the time <see cref="SourceBlock"/>'s completion
        /// resolves. Kept as a cheap safety net in case that ever changes.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// See <see cref="Dispose()"/>. Split out as the virtual half of the dispose pattern so
        /// subclasses can extend cleanup without hiding the base <see cref="_producer"/> release.
        /// Atomically swaps <see cref="_producer"/> for <c>null</c> before releasing it, so this is safe
        /// to invoke more than once - including concurrently from two different threads - since it now
        /// runs automatically on completion, and may also be called explicitly by an owner (for example
        /// via a <c>using</c> block racing the automatic dispose).
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            var producer = Interlocked.Exchange(ref _producer, null);
            try
            {
                producer?.Flush();
            }
            finally
            {
                producer?.Dispose();
            }
        }

        private sealed class ProduceEnvelope
        {
            public TInput Input { get; }
            public Task<DeliveryReport<TKafkaKey, TKafkaValue>> DeliveryTask { get; }

            public ProduceEnvelope(
                TInput input,
                Task<DeliveryReport<TKafkaKey, TKafkaValue>> deliveryTask
            )
            {
                Input = input;
                DeliveryTask = deliveryTask;
            }
        }

        /// <summary>
        /// Produces a single message truly fire-and-forget: <see cref="IProducer{TKey,TValue}.Produce"/>
        /// returns immediately, and the row is paired with the pending delivery-report task for
        /// <see cref="ConfirmAsync"/> to await in order. A synchronous failure (for example the producer not
        /// being initialized, or librdkafka's local queue being full) is captured as an already-faulted
        /// task instead of throwing here, so it is routed through <see cref="ConfirmAsync"/> the same way as an
        /// asynchronous delivery failure.
        /// </summary>
        private ProduceEnvelope Produce(TInput input)
        {
            // Check-then-init is safe without locking only because _produceBlock is created with the
            // TransformBlock default MaxDegreeOfParallelism == 1 (see EnsureBlocksCreated) and is never
            // overridden, so Produce is guaranteed to run on a single thread at a time for this instance.
            if (_producer == null)
            {
                _producer = new ProducerBuilder<TKafkaKey, TKafkaValue>(ProducerConfig).Build();
                if (!DisableLogging)
                    Logger.Debug(
                        TaskName + " was initialized!",
                        TaskType,
                        "LOG",
                        TaskHash,
                        ControlFlow.Stage,
                        ControlFlow.CurrentLoadProcess?.Id
                    );
            }
            try
            {
                return new ProduceEnvelope(input, ProduceToKafka(input));
            }
            catch (Exception e)
            {
                return new ProduceEnvelope(
                    input,
                    Task.FromException<DeliveryReport<TKafkaKey, TKafkaValue>>(e)
                );
            }
        }

        private Task<DeliveryReport<TKafkaKey, TKafkaValue>> ProduceToKafka(TInput input)
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
            return deliveryCompletion.Task;
        }

        /// <summary>
        /// Awaits a single row's delivery report strictly in the order rows were produced, so a delivery
        /// failure is attributed to the row that caused it and no later row is ever forwarded ahead of it.
        /// </summary>
        private async Task<TInput?> ConfirmAsync(ProduceEnvelope envelope)
        {
            try
            {
                var report = await envelope.DeliveryTask.ConfigureAwait(false);
                if (report.Error.IsError)
                {
                    throw new ProduceException<TKafkaKey, TKafkaValue>(report.Error, report);
                }
                LogProgress();
                return envelope.Input;
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;

                var errorData = ErrorHandler.ConvertErrorData(envelope.Input);
                ErrorHandler.Send(e, errorData);
            }
            return default;
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

        /// <summary>
        /// Constructor with producer and an injected logger, for unit testing only.
        /// </summary>
        protected KafkaTransformation(
            IProducer<string, TKafkaValue> producer,
            ILogger<KafkaTransformation<TInput, string, TKafkaValue>>? logger
        )
            : base(producer, logger) { }
    }

    /// <summary>
    /// Concrete string-valued Kafka transformation: builds the message value (and optional key) by
    /// rendering <a href="https://shopify.github.io/liquid/">Liquid</a> templates against each input row.
    /// </summary>
    /// <typeparam name="TInput">Parameters for the message templates.</typeparam>
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

        /// <summary>
        /// Renders <see cref="MessageTemplate"/> against <paramref name="input"/>.
        /// </summary>
        /// <param name="input">The row to render the template against.</param>
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

    /// <summary>
    /// Non-generic <see cref="KafkaStringTransformation{TInput}"/> for dynamic-object input rows.
    /// </summary>
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
