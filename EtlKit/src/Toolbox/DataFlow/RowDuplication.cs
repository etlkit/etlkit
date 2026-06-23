using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

using DataFlow_TypeInfo = EtlKit.Common.DataFlow.TypeInfo;
using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Creates one or more duplicates of your incoming rows.
    /// </summary>
    /// <typeparam name="TInput">Type of data input</typeparam>
    [PublicAPI]
    public class RowDuplication<TInput> : DataFlowTransformation<TInput, TInput>
    {
        /* ITask Interface */
        public override string TaskName { get; set; } = "Duplicate rows.";

        /* Public Properties */
        public int NumberOfDuplicates { get; set; } = 1;
        public Predicate<TInput> CanDuplicate { get; set; }
        public override ISourceBlock<TInput> SourceBlock => TransformBlock;
        public override ITargetBlock<TInput> TargetBlock => TransformBlock;

        /* Private stuff */
        private ObjectCopy<TInput> ObjectCopy { get; set; }
        private DataFlow_TypeInfo TypeInfo { get; set; }

        public RowDuplication()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RowDuplication([CanBeNull] ILogger<RowDuplication<TInput>> logger)
            : base(logger)
        {
            TypeInfo = new DataFlow_TypeInfo(typeof(TInput)).GatherTypeInfo();
            ObjectCopy = new ObjectCopy<TInput>(TypeInfo);
            TransformBlock = new TransformManyBlock<TInput, TInput>(
                (Func<TInput, IEnumerable<TInput>>)DuplicateRow
            );
        }

        public RowDuplication(int numberOfDuplicates)
            : this()
        {
            NumberOfDuplicates = numberOfDuplicates;
        }

        public RowDuplication(Predicate<TInput> canDuplicate, int numberOfDuplicates)
            : this(numberOfDuplicates)
        {
            CanDuplicate = canDuplicate;
        }

        public RowDuplication(Predicate<TInput> canDuplicate)
            : this()
        {
            CanDuplicate = canDuplicate;
        }

        private IEnumerable<TInput> DuplicateRow(TInput row)
        {
            if (row is null)
                return Array.Empty<TInput>();
            var result = new List<TInput>(NumberOfDuplicates) { row };
            LogProgress();
            for (var i = 0; i < NumberOfDuplicates; i++)
            {
                if (!(CanDuplicate?.Invoke(row) ?? true))
                {
                    continue;
                }

                TInput copy = ObjectCopy.Clone(row);
                result.Add(copy);
                LogProgress();
            }
            return result;
        }
    }

    /// <summary>
    /// Creates one or more duplicates of your incoming rows.
    /// The non generic implementation works with dynamic object.
    /// </summary>
    /// <see cref="RowDuplication{TInput}"/>
    [PublicAPI]
    public class RowDuplication : RowDuplication<ExpandoObject>
    {
        public RowDuplication() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public RowDuplication(ILogger<RowDuplication> logger)
            : base(logger) { }

        public RowDuplication(int numberOfDuplicates)
            : base(numberOfDuplicates) { }

        public RowDuplication(Predicate<ExpandoObject> canDuplicate, int numberOfDuplicates)
            : base(canDuplicate, numberOfDuplicates) { }

        public RowDuplication(Predicate<ExpandoObject> canDuplicate)
            : base(canDuplicate) { }
    }
}
