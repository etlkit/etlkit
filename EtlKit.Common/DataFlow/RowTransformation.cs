using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtlKit.Common.ControlFlow;
using EtlKit.Common.TaskUtilities;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Transforms the data row-by-row with the help of the transformation function.
    /// </summary>
    /// <typeparam name="TInput">Type of input data.</typeparam>
    /// <typeparam name="TOutput">Type of output data.</typeparam>
    /// <see cref="RowTransformation"/>
    /// <example>
    /// <code>
    /// RowTransformation&lt;string[], MyDataRow&gt; trans = new RowTransformation&lt;string[], MyDataRow&gt;(
    ///     csvdata => {
    ///       return new MyDataRow() { Value1 = csvdata[0], Value2 = int.Parse(csvdata[1]) };
    /// });
    /// trans.LinkTo(dest);
    /// </code>
    /// </example>
    [PublicAPI]
    public class RowTransformation<TInput, TOutput> : DataFlowTransformation<TInput, TOutput>
    {
        /* ITask Interface */
        /// <inheritdoc />
        /// <remarks>Fixed to <c>"Execute row transformation"</c>; cannot be overridden further by subclasses.</remarks>
        public sealed override string TaskName { get; set; } = "Execute row transformation";

        /* Public Properties */
        /// <summary>
        /// The function applied to each input row. Setting this property (re)creates <see
        /// cref="DataFlowTransformation{TInput,TOutput}.TransformBlock"/>; exceptions thrown by the
        /// function are routed to the linked error target if one was set via <c>LinkErrorTo</c>,
        /// otherwise rethrown.
        /// </summary>
        public Func<TInput, TOutput> TransformationFunc
        {
            get { return _transformationFunc; }
            set
            {
                _transformationFunc = value;
                TransformBlock = new TransformBlockWithCompletion<TInput, TOutput>(row =>
                {
                    try
                    {
                        return WrapTransformation(row);
                    }
                    catch (Exception e)
                    {
                        if (!ErrorHandler.HasErrorBuffer)
                            throw;
                        ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(row));
                        return default;
                    }
                })
                {
                    OnComplete = CleanUp,
                };
            }
        }

        /// <summary>
        /// Optional action run once, before the first row is transformed.
        /// </summary>
        public Action InitAction { get; set; }

        /// <summary>
        /// Whether <see cref="InitAction"/> has already run.
        /// </summary>
        public bool WasInitialized { get; private set; }

        /// <inheritdoc />
        public override ITargetBlock<TInput> TargetBlock => TransformBlock;

        /// <inheritdoc />
        public override ISourceBlock<TOutput> SourceBlock => TransformBlock;

        /* Private stuff */
        private Func<TInput, TOutput> _transformationFunc;

        /// <summary>
        /// Creates a new instance with no transformation function set yet.
        /// </summary>
        public RowTransformation() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RowTransformation(ILogger<RowTransformation<TInput, TOutput>> logger)
            : base(logger) { }

        /// <summary>
        /// Creates a new instance with the given transformation function.
        /// </summary>
        /// <param name="rowTransformationFunc">Function applied to each input row.</param>
        public RowTransformation(Func<TInput, TOutput> rowTransformationFunc)
            : this()
        {
            TransformationFunc = rowTransformationFunc;
        }

        /// <summary>
        /// Creates a new instance with the given task name and transformation function.
        /// </summary>
        /// <param name="name">Task name to use instead of the default.</param>
        /// <param name="rowTransformationFunc">Function applied to each input row.</param>
        public RowTransformation(string name, Func<TInput, TOutput> rowTransformationFunc)
            : this(rowTransformationFunc)
        {
            TaskName = name;
        }

        /// <summary>
        /// Creates a new instance with the given task name, transformation function, and one-time
        /// initialization action.
        /// </summary>
        /// <param name="name">Task name to use instead of the default.</param>
        /// <param name="rowTransformationFunc">Function applied to each input row.</param>
        /// <param name="initAction">Action run once, before the first row is transformed.</param>
        public RowTransformation(
            string name,
            Func<TInput, TOutput> rowTransformationFunc,
            Action initAction
        )
            : this(rowTransformationFunc)
        {
            TaskName = name;
            InitAction = initAction;
        }

        /// <summary>
        /// Creates a new instance, copying identity and logging settings from <paramref name="task"/>.
        /// </summary>
        /// <param name="task">The task to copy properties from.</param>
        internal RowTransformation(ITask task)
            : this()
        {
            CopyTaskProperties(task);
        }

        /// <summary>
        /// Creates a new instance with the given transformation function, copying identity and
        /// logging settings from <paramref name="task"/>.
        /// </summary>
        /// <param name="task">The task to copy properties from.</param>
        /// <param name="rowTransformationFunc">Function applied to each input row.</param>
        internal RowTransformation(ITask task, Func<TInput, TOutput> rowTransformationFunc)
            : this(rowTransformationFunc)
        {
            CopyTaskProperties(task);
        }

        private TOutput WrapTransformation(TInput row)
        {
            if (!WasInitialized)
            {
                InitAction?.Invoke();
                WasInitialized = true;
                if (!DisableLogging)
                    Logger.Debug(
                        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                        TaskName + " was initialized!",
                        TaskType,
                        "LOG",
                        TaskHash,
                        Common.ControlFlow.ControlFlow.Stage,
                        Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
                    );
            }
            LogProgress();
            return TransformationFunc.Invoke(row);
        }

        /// <summary>
        /// Called once the transform block completes. Does nothing by default; derived classes
        /// override to react to completion.
        /// </summary>
        /// <param name="transformTask">The completed (or faulted) transform block task.</param>
        protected virtual void CleanUp(Task transformTask)
        {
            // Do nothing by default.
        }
    }

    /// <summary>
    /// Transforms the data row-by-row with the help of the transformation function.
    /// </summary>
    /// <typeparam name="TInput">Type of input (and output) data.</typeparam>
    /// <see cref="RowTransformation{TInput, TOutput}"/>
    /// <example>
    /// <code>
    /// RowTransformation&lt;MyDataRow&gt; trans = new RowTransformation&lt;MyDataRow&gt;(
    ///     row => {
    ///       row.Value += 1;
    ///       return row;
    /// });
    /// trans.LinkTo(dest);
    /// </code>
    /// </example>
    [PublicAPI]
    public class RowTransformation<TInput> : RowTransformation<TInput, TInput>
    {
        /// <inheritdoc cref="RowTransformation{TInput,TOutput}.RowTransformation()" />
        public RowTransformation() { }

        /// <inheritdoc cref="RowTransformation{TInput,TOutput}.RowTransformation(ILogger{RowTransformation{TInput,TOutput}})" />
        public RowTransformation(ILogger<RowTransformation<TInput>> logger)
            : base(logger) { }

        /// <inheritdoc cref="RowTransformation{TInput,TOutput}.RowTransformation(Func{TInput,TOutput})" />
        public RowTransformation(Func<TInput, TInput> rowTransformationFunc)
            : base(rowTransformationFunc) { }

        /// <inheritdoc cref="RowTransformation{TInput,TOutput}.RowTransformation(string,Func{TInput,TOutput})" />
        public RowTransformation(string name, Func<TInput, TInput> rowTransformationFunc)
            : base(name, rowTransformationFunc) { }

        /// <inheritdoc cref="RowTransformation{TInput,TOutput}.RowTransformation(string,Func{TInput,TOutput},Action)" />
        public RowTransformation(
            string name,
            Func<TInput, TInput> rowTransformationFunc,
            Action initAction
        )
            : base(name, rowTransformationFunc, initAction) { }
    }

    /// <summary>
    /// Transforms the data row-by-row with the help of the transformation function.
    /// The non generic RowTransformation accepts a dynamic object as input and returns a dynamic object as output.
    /// If you need other data types, use the generic RowTransformation instead.
    /// </summary>
    /// <see cref="RowTransformation{TInput, TOutput}"/>
    /// <example>
    /// <code>
    /// //Non generic RowTransformation works with dynamic object as input and output
    /// //use RowTransformation&lt;TInput,TOutput&gt; for generic usage!
    /// RowTransformation trans = new RowTransformation(
    ///     csvdata => {
    ///       return new string[] { csvdata[0],  int.Parse(csvdata[1]) };
    /// });
    /// trans.LinkTo(dest);
    /// </code>
    /// </example>
    [PublicAPI]
    public class RowTransformation : RowTransformation<ExpandoObject>
    {
        /// <inheritdoc cref="RowTransformation{TInput}.RowTransformation()" />
        public RowTransformation() { }

        /// <inheritdoc cref="RowTransformation{TInput}.RowTransformation(ILogger{RowTransformation{TInput}})" />
        public RowTransformation(ILogger<RowTransformation> logger)
            : base(logger) { }

        /// <inheritdoc cref="RowTransformation{TInput}.RowTransformation(Func{TInput,TInput})" />
        public RowTransformation(Func<ExpandoObject, ExpandoObject> rowTransformationFunc)
            : base(rowTransformationFunc) { }

        /// <inheritdoc cref="RowTransformation{TInput}.RowTransformation(string,Func{TInput,TInput})" />
        public RowTransformation(
            string name,
            Func<ExpandoObject, ExpandoObject> rowTransformationFunc
        )
            : base(name, rowTransformationFunc) { }

        /// <inheritdoc cref="RowTransformation{TInput}.RowTransformation(string,Func{TInput,TInput},Action)" />
        public RowTransformation(
            string name,
            Func<ExpandoObject, ExpandoObject> rowTransformationFunc,
            Action initAction
        )
            : base(name, rowTransformationFunc, initAction) { }
    }
}
