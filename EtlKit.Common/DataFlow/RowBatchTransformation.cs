using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow;

/// <summary>
/// Batch transformation built on top of TPL Dataflow. Incoming rows are accumulated
/// into fixed-size batches and processed together. Results are published one-by-one
/// while preserving the order within the batch.
/// </summary>
[PublicAPI]
public abstract class RowBatchTransformation<TInput, TOutput>
    : DataFlowTransformation<TInput, TOutput>
{
    /// <summary>
    /// Callback invoked before the batch transformation. You can modify the input array here.
    /// </summary>
    public Func<TInput[], TInput[]> BeforeBatchTransform { get; set; }

    /// <summary>
    /// Mandatory batch transformation function. Must return an array of results.
    /// Note: downstream logic assumes the function returns non-null.
    /// </summary>
    public Func<TInput[], TOutput[]> BatchTransform
    {
        get => _batchTransform ?? throw new InvalidOperationException("BatchTransform не задан.");
        set => _batchTransform = value;
    }

    /// <summary>
    /// Callback invoked after a successful batch transformation. Receives copies of input and output arrays.
    /// </summary>
    public Action<TInput[], TOutput[]> AfterBatchTransform { get; set; }

    /// <summary>
    /// Batch size. Default value is <see cref="DefaultBatchSize"/>.
    /// </summary>
    public int BatchSize
    {
        get => _batchSize ?? DefaultBatchSize;
        set
        {
            _batchSize = value > 0 ? value : null;
            InitObjects(_batchSize ?? DefaultBatchSize);
        }
    }

    public const int DefaultBatchSize = 1000;

    /// <summary>
    /// Maximum number of messages in the internal buffers. Defaults to 3 * BatchSize.
    /// </summary>
    public int BoundedCapacity
    {
        get => _boundedCapacity ?? BatchSize * 3;
        set
        {
            _boundedCapacity = value;
            InitObjects(BatchSize);
        }
    }

    /// <summary>
    /// One-time initialization action, executed before processing the first batch.
    /// </summary>
    public Action InitAction { get; set; }

    /// <summary>
    /// Indicates if initialization has already been executed.
    /// </summary>
    public bool WasInitialized { get; private set; }

    public override ITargetBlock<TInput> TargetBlock => TransformBlock;
    public override ISourceBlock<TOutput> SourceBlock => TransformBlock;

    private int? _batchSize;
    private int? _boundedCapacity;
    private Func<TInput[], TOutput[]> _batchTransform;

    // Output/source block — stored as a field to send each result item to downstream
    private BufferBlock<TOutput> _output = null!;

    protected RowBatchTransformation()
    {
        InitObjects(BatchSize);
    }

    /// <summary>
    /// Creates a new instance with an injected logger.
    /// </summary>
    protected RowBatchTransformation(ILogger logger)
        : base(logger)
    {
        InitObjects(BatchSize);
    }

    private void InitObjects(int initBatchSize)
    {
        var groupingOptions = new GroupingDataflowBlockOptions
        {
            BoundedCapacity = BoundedCapacity,
        };
        var execOptions = new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1,
            MaxDegreeOfParallelism = 1,
        };

        var buffer = new BatchBlock<TInput>(initBatchSize, groupingOptions);
        _output = new BufferBlock<TOutput>(
            new DataflowBlockOptions { BoundedCapacity = BoundedCapacity }
        );

        var executor = new ActionBlock<TInput[]>(
            async data => await ExecuteBatchAsync(data).ConfigureAwait(false),
            execOptions
        );

        // Link the batch buffer to the executor; propagate completion
        buffer.LinkTo(executor, new DataflowLinkOptions { PropagateCompletion = true });

        // When executor completes, complete or fault the output accordingly
        executor.Completion.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                ((IDataflowBlock)_output).Fault(t.Exception!.InnerException!);
            }
            else
            {
                _output.Complete();
            }
            CleanUp();
        });

        // Encapsulate into a single propagator for compatibility with the ETLBox infrastructure
        TransformBlock = DataflowBlock.Encapsulate<TInput, TOutput>(buffer, _output);
    }

    private async Task ExecuteBatchAsync(TInput[] data)
    {
        if (ProgressCount == 0)
            LogStart();

        if (!WasInitialized)
        {
            InitAction?.Invoke();
            WasInitialized = true;
        }

        if (BeforeBatchTransform != null)
        {
            data = BeforeBatchTransform.Invoke(data);
        }

        TOutput[] results;
        try
        {
            results = BatchTransform.Invoke(data);
            if (results == null)
                throw new InvalidOperationException("BatchTransform вернул null.");
        }
        catch (Exception ex)
        {
            if (!ErrorHandler.HasErrorBuffer)
                throw;

            foreach (var item in data)
            {
                ErrorHandler.Send(ex, ErrorHandler.ConvertErrorData(item));
            }
            return; // Не публикуем результатов для этого батча
        }

        // Publish results one-by-one, preserving order within the batch
        foreach (var r in results)
        {
            await _output.SendAsync(r).ConfigureAwait(false);
        }

        // Progress is counted by the number of actually published results
        LogProgressBatch(results.Length);
        AfterBatchTransform?.Invoke((TInput[])data.Clone(), (TOutput[])results.Clone());
    }

    /// <summary>
    /// Called on completion — override to release resources if needed.
    /// </summary>
    protected virtual void CleanUp()
    {
        // No-op by default — the base DataFlowTask will handle final logging via Complete/Progress
    }
}
