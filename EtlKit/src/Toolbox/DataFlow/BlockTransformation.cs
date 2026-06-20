using EtlKit.Primitives;

using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// A block transformation will wait for all data to be loaded into the buffer before the transformation is applied. After all data is in the buffer, the transformation
    /// is execution and the result posted into the targets.
    /// </summary>
    /// <typeparam name="TInput">Type of data input</typeparam>
    /// <typeparam name="TOutput">Type of data output</typeparam>
    /// <example>
    /// <code>
    /// BlockTransformation&lt;MyInputRow, MyOutputRow&gt; block = new BlockTransformation&lt;MyInputRow, MyOutputRow&gt;(
    ///     inputDataAsList => {
    ///         return inputData.Select( inputRow => new MyOutputRow() { Value2 = inputRow.Value1 }).ToList();
    ///     });
    /// block.LinkTo(dest);
    /// </code>
    /// </example>
    [PublicAPI]
    public class BlockTransformation<TInput, TOutput> : DataFlowTransformation<TInput, TOutput>
    {
        /* ITask Interface */
        public sealed override string TaskName { get; set; } = "Excecute block transformation";

        /* Public Properties */
        public Func<List<TInput>, List<TOutput>> BlockTransformationFunc
        {
            get { return _blockTransformationFunc; }
            set
            {
                _blockTransformationFunc = value;
                InputBuffer = new ActionBlock<TInput>(row => InputData.Add(row));
                InputBuffer.Completion.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        ((IDataflowBlock)OutputBuffer).Fault(t.Exception!.InnerException!);
                    try
                    {
                        WriteIntoOutput();
                        OutputBuffer.Complete();
                    }
                    catch (Exception e)
                    {
                        ((IDataflowBlock)OutputBuffer).Fault(e);
                        throw;
                    }
                });
            }
        }
        public override ISourceBlock<TOutput> SourceBlock => OutputBuffer;
        public override ITargetBlock<TInput> TargetBlock => InputBuffer;

        /* Private stuff */
        private BufferBlock<TOutput> OutputBuffer { get; set; }
        private ActionBlock<TInput> InputBuffer { get; set; }
        private Func<List<TInput>, List<TOutput>> _blockTransformationFunc;
        private List<TInput> InputData { get; set; }
        private List<TOutput> OutputData { get; set; }

        public BlockTransformation()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public BlockTransformation([CanBeNull] ILogger<BlockTransformation<TInput, TOutput>> logger)
            : base(logger)
        {
            InputData = new List<TInput>();
            OutputBuffer = new BufferBlock<TOutput>();
        }

        public BlockTransformation(Func<List<TInput>, List<TOutput>> blockTransformationFunc)
            : this()
        {
            BlockTransformationFunc = blockTransformationFunc;
        }

        public BlockTransformation(
            string name,
            Func<List<TInput>, List<TOutput>> blockTransformationFunc
        )
            : this(blockTransformationFunc)
        {
            TaskName = name;
        }

        internal BlockTransformation(
            ITask task,
            Func<List<TInput>, List<TOutput>> blockTransformationFunc
        )
            : this(blockTransformationFunc)
        {
            CopyTaskProperties(task);
        }

        private void WriteIntoOutput()
        {
            LogStart();
            OutputData = BlockTransformationFunc(InputData);
            foreach (TOutput row in OutputData)
            {
                OutputBuffer.SendAsync(row).Wait();
                LogProgress();
            }
            LogFinish();
        }
    }

    /// <summary>
    /// A block transformation will wait for all data to be loaded into the buffer before the transformation is applied. After all data is in the buffer, the transformation
    /// is execution and the result posted into the targets.
    /// </summary>
    /// <typeparam name="TInput">Type of data input (equal type of data output)</typeparam>
    /// <example>
    /// <code>
    /// BlockTransformation&lt;MyDataRow&gt; block = new BlockTransformation&lt;MyDataRow&gt;(
    ///     inputData => {
    ///         return inputData.Select( row => new MyDataRow() { Value1 = row.Value1, Value2 = 3 }).ToList();
    ///     });
    /// block.LinkTo(dest);
    /// </code>
    /// </example>
    [PublicAPI]
    public class BlockTransformation<TInput> : BlockTransformation<TInput, TInput>
    {
        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public BlockTransformation([CanBeNull] ILogger<BlockTransformation<TInput>> logger)
            : base(logger) { }

        public BlockTransformation(Func<List<TInput>, List<TInput>> blockTransformationFunc)
            : base(blockTransformationFunc) { }

        public BlockTransformation(
            string name,
            Func<List<TInput>, List<TInput>> blockTransformationFunc
        )
            : base(name, blockTransformationFunc) { }
    }

    /// <summary>
    /// A block transformation will wait for all data to be loaded into the buffer before the transformation is applied. After all data is in the buffer, the transformation
    /// is execution and the result posted into the targets.
    /// The non generic implementation uses dynamic objects as input and output
    /// </summary>
    [PublicAPI]
    public class BlockTransformation : BlockTransformation<ExpandoObject>
    {
        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public BlockTransformation(ILogger<BlockTransformation> logger)
            : base(logger) { }

        public BlockTransformation(
            Func<List<ExpandoObject>, List<ExpandoObject>> blockTransformationFunc
        )
            : base(blockTransformationFunc) { }

        public BlockTransformation(
            string name,
            Func<List<ExpandoObject>, List<ExpandoObject>> blockTransformationFunc
        )
            : base(name, blockTransformationFunc) { }
    }
}
