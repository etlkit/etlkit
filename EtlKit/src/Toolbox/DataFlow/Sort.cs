using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Sort the input with the given sort function.
    /// </summary>
    /// <typeparam name="TInput">Type of input data (equal type of output data).</typeparam>
    /// <example>
    /// <code>
    /// Comparison&lt;MyDataRow&gt; comp = new Comparison&lt;MyDataRow&gt;(
    ///     (x, y) => y.Value2 - x.Value2
    /// );
    /// Sort&lt;MyDataRow&gt; block = new Sort&lt;MyDataRow&gt;(comp);
    /// </code>
    /// </example>
    [PublicAPI]
    public class Sort<TInput> : DataFlowTransformation<TInput, TInput>
    {
        /* ITask Interface */
        public sealed override string TaskName { get; set; } = "Sort";

        /* Public Properties */

        public Comparison<TInput> SortFunction
        {
            get { return _sortFunction; }
            set
            {
                _sortFunction = value;
                BlockTransformation = new BlockTransformation<TInput, TInput>(this, SortByFunc);
            }
        }

        public override ISourceBlock<TInput> SourceBlock => BlockTransformation.SourceBlock;
        public override ITargetBlock<TInput> TargetBlock => BlockTransformation.TargetBlock;

        /* Private stuff */
        private Comparison<TInput> _sortFunction;
        private BlockTransformation<TInput, TInput> BlockTransformation { get; set; }

        public Sort() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public Sort(ILogger<Sort<TInput>> logger)
            : base(logger) { }

        public Sort(Comparison<TInput> sortFunction)
            : this()
        {
            SortFunction = sortFunction;
        }

        public Sort(string name, Comparison<TInput> sortFunction)
            : this(sortFunction)
        {
            TaskName = name;
        }

        private List<TInput> SortByFunc(List<TInput> data)
        {
            data.Sort(SortFunction);
            return data;
        }
    }

    /// <summary>
    /// Sort the input with the given sort function. The non generic implementation works with a dyanmic object.
    /// </summary>
    [PublicAPI]
    public class Sort : Sort<ExpandoObject>
    {
        public Sort() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public Sort(ILogger<Sort> logger)
            : base(logger) { }

        public Sort(Comparison<ExpandoObject> sortFunction)
            : base(sortFunction) { }

        public Sort(string name, Comparison<ExpandoObject> sortFunction)
            : base(name, sortFunction) { }
    }
}
