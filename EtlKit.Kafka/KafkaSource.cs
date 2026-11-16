using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Confluent.Kafka;
using EtlKit.Common.DataFlow;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow;

/// <summary>
/// Kafka generic source that gives the conversion access to the whole consumed message:
/// key, value, timestamp, headers, partition and offset.
/// </summary>
/// <typeparam name="TOutput">Result type</typeparam>
/// <typeparam name="TKafkaKey">Kafka key type; use <see cref="Ignore"/> to skip the key</typeparam>
/// <typeparam name="TKafkaValue">Kafka value type</typeparam>
[PublicAPI]
public abstract class KafkaSource<TOutput, TKafkaKey, TKafkaValue>
    : DataFlowSource<TOutput>,
        IDataFlowSource<TOutput>
{
    /// <summary>
    /// Kafka consumer configuration
    /// </summary>
    public ConsumerConfig ConsumerConfig { get; set; } = new();

    /// <summary>
    /// Topic name to subscribe to
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Additional configuration for the consumer builder, before building consumer
    /// </summary>
    public Action<ConsumerBuilder<TKafkaKey, TKafkaValue>>? ConfigureConsumerBuilder { get; set; }

    /// <summary>
    /// Override this method to convert the consumed Kafka message to the output value.
    /// Messages with a <c>null</c> value are skipped before this method is called.
    /// </summary>
    /// <param name="consumeResult">message as read from Kafka consumer, with key, value, timestamp, headers, partition and offset</param>
    /// <param name="logRowOnError">conversion error handler; accepts the exception and a text representation of the failed message</param>
    /// <returns>Output value to put to destination or null in case of error</returns>
    protected abstract TOutput? ConvertConsumeResult(
        ConsumeResult<TKafkaKey, TKafkaValue> consumeResult,
        Action<Exception, string>? logRowOnError = null
    );

    /// <summary>
    /// Default constructor
    /// </summary>
    protected KafkaSource() { }

    /// <summary>
    /// Creates a new instance with an injected logger.
    /// </summary>
    protected KafkaSource(ILogger? logger)
        : base(logger) { }

    /// <summary>
    /// Main execution method
    /// </summary>
    /// <param name="cancellationToken"></param>
    public override void Execute(CancellationToken cancellationToken)
    {
        LogStart();

        var builder = new ConsumerBuilder<TKafkaKey, TKafkaValue>(ConsumerConfig);
        ConfigureConsumerBuilder?.Invoke(builder);
        using var consumer = builder.Build();

        consumer.Subscribe(Topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!ConsumeAndSendSingleMessage(consumer, cancellationToken))
                {
                    break;
                }
            }
        }
        finally
        {
            Buffer.Complete();
            LogFinish();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Consume, convert and send a single message
    /// </summary>
    /// <param name="consumer"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>false to exit reading cycle, true to continue</returns>
    private bool ConsumeAndSendSingleMessage(
        IConsumer<TKafkaKey, TKafkaValue> consumer,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var consumeResult = consumer.Consume(cancellationToken);
            if (consumeResult.IsPartitionEOF)
            {
                // Return false if we reached end of partition
                return false;
            }

            if (consumeResult.Message.Value is null)
            {
                // Nulls do not propagate through the pipeline, just skip them
                return true;
            }

            var outputValue = ConvertConsumeResult(
                consumeResult,
                (exception, row) =>
                {
                    if (ErrorHandler.HasErrorBuffer)
                        ErrorHandler.Send(exception, row);
                }
            );
            if (outputValue is null)
            {
                return true;
            }

            // We do not pass cancellation token to SendAsync because we want write operation to complete before canceling
            // ReSharper disable once MethodSupportsCancellation
#pragma warning disable CA2016
            Buffer.SendAsync(outputValue, CancellationToken.None).Wait();
#pragma warning restore CA2016
            LogProgress();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (!ErrorHandler.HasErrorBuffer)
        {
            throw;
        }
        catch (ConsumeException ex)
        {
            ErrorHandler.Send(
                ex,
                $"Offset: {ex.ConsumerRecord.Offset} -- Key(base64): {Convert.ToBase64String(ex.ConsumerRecord.Message.Key)} -- Value(base64): {Convert.ToBase64String(ex.ConsumerRecord.Message.Value)}"
            );
        }
        catch (Exception e)
        {
            ErrorHandler.Send(e, "N/A");
        }
        return true;
    }
}

/// <summary>
/// Kafka generic source that ignores the message key and converts only the message value.
/// Derive from <see cref="KafkaSource{TOutput, TKafkaKey, TKafkaValue}"/> to read the key,
/// timestamp, headers, partition or offset.
/// </summary>
/// <typeparam name="TOutput">Result type</typeparam>
/// <typeparam name="TKafkaValue">Kafka value type</typeparam>
[PublicAPI]
public abstract class KafkaSource<TOutput, TKafkaValue> : KafkaSource<TOutput, Ignore, TKafkaValue>
{
    /// <summary>
    /// Override this method to convert the Kafka value to the output value
    /// </summary>
    /// <param name="kafkaValue">value as read from Kafka consumer</param>
    /// <param name="logRowOnError">conversion error handler, should accept Exception and Json representation of input data</param>
    /// <returns>Output value to put to destination or null in case of error</returns>
    protected abstract TOutput? ConvertToOutputValue(
        TKafkaValue kafkaValue,
        Action<Exception, string>? logRowOnError = null
    );

    /// <summary>
    /// Default constructor
    /// </summary>
    protected KafkaSource() { }

    /// <summary>
    /// Creates a new instance with an injected logger.
    /// </summary>
    protected KafkaSource(ILogger<KafkaSource<TOutput, TKafkaValue>>? logger)
        : base(logger) { }

    /// <summary>
    /// Converts the message value with <see cref="ConvertToOutputValue"/>; the key and the other
    /// message metadata are ignored.
    /// </summary>
    /// <param name="consumeResult">message as read from Kafka consumer</param>
    /// <param name="logRowOnError">conversion error handler; accepts the exception and a text representation of the failed message</param>
    /// <returns>Output value to put to destination or null in case of error</returns>
    protected override TOutput? ConvertConsumeResult(
        ConsumeResult<Ignore, TKafkaValue> consumeResult,
        Action<Exception, string>? logRowOnError = null
    ) => ConvertToOutputValue(consumeResult.Message.Value, logRowOnError);
}
