using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Drops rows from the flow based on a predicate. Rows for which the predicate
    /// returns <c>true</c> are forwarded to the output, others are discarded.
    /// Non-blocking transformation; internally a <see cref="TransformManyBlock{TInput,TOutput}"/>
    /// returning a single-element array on a passing row and an empty array otherwise.
    /// </summary>
    /// <typeparam name="TInput">Type of data input and output.</typeparam>
    /// <remarks>
    /// <para>
    /// <c>null</c> rows are dropped silently without invoking the predicate.
    /// If the predicate throws and an error destination is linked via <c>LinkErrorTo</c>,
    /// the failing row is forwarded there with the exception attached; without an error
    /// destination the exception propagates and stops the flow.
    /// </para>
    /// <para>
    /// The component is intentionally distinct from <c>RowMultiplication</c>. Multiplication
    /// can technically filter by returning an empty array, but its name and signature read
    /// as "one to N", and the empty-array case is the inverse use. <c>RowFiltration</c>
    /// keeps the predicate-shaped API at the call site and centralises the try/catch around
    /// the predicate.
    /// </para>
    /// <para>
    /// See <c>docs/dataflow/row-filtration.md</c> for examples, error handling and the
    /// expression-based variant <c>ExpressionRowFiltration</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// DbSource&lt;MySimpleRow&gt; source = new DbSource&lt;MySimpleRow&gt;("SourceTable");
    /// RowFiltration&lt;MySimpleRow&gt; filtration = new RowFiltration&lt;MySimpleRow&gt;(row => row.Col1 > 0);
    /// DbDestination&lt;MySimpleRow&gt; dest = new DbDestination&lt;MySimpleRow&gt;("DestTable");
    ///
    /// source.LinkTo(filtration);
    /// filtration.LinkTo(dest);
    /// </code>
    /// </example>
    [PublicAPI]
    public class RowFiltration<TInput> : DataFlowTransformation<TInput, TInput>
    {
        /* ITask Interface */
        public override string TaskName { get; set; } = "Filter rows.";

        /* Public Properties */
        public override ISourceBlock<TInput> SourceBlock => TransformBlock;
        public override ITargetBlock<TInput> TargetBlock => TransformBlock;

        /// <summary>
        /// Predicate function that determines whether a row should pass through.
        /// Returns true to keep the row, false to discard it.
        /// </summary>
        public Func<TInput, bool> PredicateFunc { get; set; }

        /* Constructors */
        public RowFiltration(Func<TInput, bool> predicateFunc)
            : this()
        {
            PredicateFunc = predicateFunc;
        }

        public RowFiltration()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RowFiltration([CanBeNull] ILogger<RowFiltration<TInput>> logger)
            : base(logger)
        {
            TransformBlock = new TransformManyBlock<TInput, TInput>(
                (Func<TInput, IEnumerable<TInput>>)FilterRow
            );
        }

        private IEnumerable<TInput> FilterRow(TInput row)
        {
            if (row is null)
                return Array.Empty<TInput>();
            if (PredicateFunc is null)
                throw new InvalidOperationException(
                    "PredicateFunc is not set. Provide a predicate via the constructor or by assigning the property before running the flow."
                );
            try
            {
                return PredicateFunc.Invoke(row) ? [row] : Array.Empty<TInput>();
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(row));
                return Array.Empty<TInput>();
            }
        }
    }

    /// <summary>
    /// Non-generic <see cref="RowFiltration{TInput}"/> bound to <see cref="ExpandoObject"/>.
    /// Used by the XML reader and by flows on dynamic rows.
    /// </summary>
    /// <example>
    /// <code>
    /// RowFiltration filtration = new RowFiltration(row => ((dynamic)row).Col1 > 0);
    /// </code>
    /// </example>
    /// <seealso cref="RowFiltration{TInput}"/>
    [PublicAPI]
    public class RowFiltration : RowFiltration<ExpandoObject>
    {
        public RowFiltration() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RowFiltration(ILogger<RowFiltration> logger)
            : base(logger) { }

        public RowFiltration(Func<ExpandoObject, bool> predicateFunc)
            : base(predicateFunc) { }
    }
}
