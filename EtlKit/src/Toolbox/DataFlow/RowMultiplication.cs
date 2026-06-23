using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// This transformation allow you to transform your input data into multple output data records.
    /// </summary>
    /// <typeparam name="TInput">Type of data input</typeparam>
    /// <typeparam name="TOutput">Type of data output</typeparam>
    [PublicAPI]
    public class RowMultiplication<TInput, TOutput> : DataFlowTransformation<TInput, TOutput>
    {
        /* ITask Interface */
        public override string TaskName { get; set; } = "Duplicate rows.";

        /* Public Properties */
        public override ISourceBlock<TOutput> SourceBlock => TransformBlock;
        public override ITargetBlock<TInput> TargetBlock => TransformBlock;
        public Func<TInput, IEnumerable<TOutput>> MultiplicationFunc { get; set; }

        /* Private stuff */
        public RowMultiplication(Func<TInput, IEnumerable<TOutput>> multiplicationFunc)
            : this()
        {
            MultiplicationFunc = multiplicationFunc;
        }

        public RowMultiplication()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RowMultiplication([CanBeNull] ILogger<RowMultiplication<TInput, TOutput>> logger)
            : base(logger)
        {
            TransformBlock = new TransformManyBlock<TInput, TOutput>(
                (Func<TInput, IEnumerable<TOutput>>)MultiplyRow
            );
        }

        private IEnumerable<TOutput> MultiplyRow(TInput row)
        {
            if (row is null)
                return Array.Empty<TOutput>();
            try
            {
                return MultiplicationFunc.Invoke(row);
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(row));
                return Array.Empty<TOutput>();
            }
        }
    }

    /// <summary>
    /// This transformation allow you to transform your input data into multple output data records.
    /// </summary>
    /// <see cref="RowMultiplication{TInput, TOutput}"/>
    [PublicAPI]
    public class RowMultiplication : RowMultiplication<ExpandoObject, ExpandoObject>
    {
        public RowMultiplication() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RowMultiplication(ILogger<RowMultiplication> logger)
            : base(logger) { }

        public RowMultiplication(Func<ExpandoObject, IEnumerable<ExpandoObject>> multiplicationFunc)
            : base(multiplicationFunc) { }
    }

    /// <inheritdoc/>
    [PublicAPI]
    public class RowMultiplication<TInput> : RowMultiplication<TInput, TInput>
    {
        public RowMultiplication() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RowMultiplication(ILogger<RowMultiplication<TInput>> logger)
            : base(logger) { }

        public RowMultiplication(Func<TInput, IEnumerable<TInput>> multiplicationFunc)
            : base(multiplicationFunc) { }
    }
}
