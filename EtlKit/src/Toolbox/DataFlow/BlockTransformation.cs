using EtlKit.Common.DataFlow;
using EtlKit.Primitives;
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
        /// <inheritdoc />
        /// <remarks>Fixed to <c>"Execute block transformation"</c>; cannot be overridden further by subclasses.</remarks>
        public sealed override string TaskName { get; set; } = "Execute block transformation";

        /* Public Properties */
        /// <summary>
        /// The function applied to the full buffered input list once all rows have arrived. Setting
        /// this property wires up the input buffer.
        /// </summary>
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

        /// <summary>
        /// Creates a new instance with no transformation function set yet.
        /// </summary>
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

        /// <summary>
        /// Creates a new instance with the given block transformation function.
        /// </summary>
        /// <param name="blockTransformationFunc">Function applied to the full buffered input list.</param>
        public BlockTransformation(Func<List<TInput>, List<TOutput>> blockTransformationFunc)
            : this()
        {
            BlockTransformationFunc = blockTransformationFunc;
        }

        /// <summary>
        /// Creates a new instance with the given task name and block transformation function.
        /// </summary>
        /// <param name="name">Task name to use instead of the default.</param>
        /// <param name="blockTransformationFunc">Function applied to the full buffered input list.</param>
        public BlockTransformation(
            string name,
            Func<List<TInput>, List<TOutput>> blockTransformationFunc
        )
            : this(blockTransformationFunc)
        {
            TaskName = name;
        }

        /// <summary>
        /// Creates a new instance with the given block transformation function, copying identity and
        /// logging settings from <paramref name="task"/>.
        /// </summary>
        /// <param name="task">The task to copy properties from.</param>
        /// <param name="blockTransformationFunc">Function applied to the full buffered input list.</param>
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
        /// <inheritdoc cref="BlockTransformation{TInput,TOutput}.BlockTransformation(ILogger{BlockTransformation{TInput,TOutput}})" />
        public BlockTransformation([CanBeNull] ILogger<BlockTransformation<TInput>> logger)
            : base(logger) { }

        /// <inheritdoc cref="BlockTransformation{TInput,TOutput}.BlockTransformation(Func{List{TInput},List{TOutput}})" />
        public BlockTransformation(Func<List<TInput>, List<TInput>> blockTransformationFunc)
            : base(blockTransformationFunc) { }

        /// <inheritdoc cref="BlockTransformation{TInput,TOutput}.BlockTransformation(string,Func{List{TInput},List{TOutput}})" />
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
        /// <inheritdoc cref="BlockTransformation{TInput}.BlockTransformation(ILogger{BlockTransformation{TInput}})" />
        public BlockTransformation(ILogger<BlockTransformation> logger)
            : base(logger) { }

        /// <inheritdoc cref="BlockTransformation{TInput}.BlockTransformation(Func{List{TInput},List{TInput}})" />
        public BlockTransformation(
            Func<List<ExpandoObject>, List<ExpandoObject>> blockTransformationFunc
        )
            : base(blockTransformationFunc) { }

        /// <inheritdoc cref="BlockTransformation{TInput}.BlockTransformation(string,Func{List{TInput},List{TInput}})" />
        public BlockTransformation(
            string name,
            Func<List<ExpandoObject>, List<ExpandoObject>> blockTransformationFunc
        )
            : base(name, blockTransformationFunc) { }
    }
}
