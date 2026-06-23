using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;

using Confluent.Kafka;
using EtlKit.Primitives;

using EtlKit.Common.DataFlow;

using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow;

/// <summary>
/// Kafka generic source
/// </summary>
/// <typeparam name="TOutput">Result type</typeparam>
/// <typeparam name="TKafkaValue">Kafka value type</typeparam>
[PublicAPI]
public abstract class KafkaSource<TOutput, TKafkaValue>
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
    public Action<ConsumerBuilder<Ignore, TKafkaValue>>? ConfigureConsumerBuilder { get; set; }

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
    /// Main execution method
    /// </summary>
    /// <param name="cancellationToken"></param>
    public override void Execute(CancellationToken cancellationToken)
    {
        LogStart();

        var builder = new ConsumerBuilder<Ignore, TKafkaValue>(ConsumerConfig);
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
        IConsumer<Ignore, TKafkaValue> consumer,
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

            var kafkaValue = consumeResult.Message.Value;
            if (kafkaValue is null)
            {
                // Nulls do not propagate through the pipeline, just skip them
                return true;
            }

            var outputValue = ConvertToOutputValue(
                kafkaValue,
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
