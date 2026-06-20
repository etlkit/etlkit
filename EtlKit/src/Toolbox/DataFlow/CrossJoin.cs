using System.Threading;

using EtlKit.Primitives;

using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Will cross join data from the two inputs into one output. The input for the first table will be loaded into memory before the actual
    /// join can start. After this, every incoming row will be joined with every row of the InMemory-Table by the given function CrossJoinFunc.
    /// The InMemory target should always have the smaller amount of data to reduce memory consumption and processing time.
    /// </summary>
    /// <typeparam name="TInput1">Type of data for in memory input block.</typeparam>
    /// <typeparam name="TInput2">Type of data for processing input block.</typeparam>
    /// <typeparam name="TOutput">Type of output data.</typeparam>
    [PublicAPI]
    public class CrossJoin<TInput1, TInput2, TOutput>
        : DataFlowSource<TOutput>,
            IDataFlowLinkSource<TOutput>
    {
        /* ITask Interface */
        public override string TaskName { get; set; } = "Cross join data";
        public IEnumerable<TInput1> InMemoryData => InMemoryTarget.Data;
        public MemoryDestination<TInput1> InMemoryTarget { get; set; }
        public CustomDestination<TInput2> PassingTarget { get; set; }
        public Func<TInput1, TInput2, TOutput> CrossJoinFunc { get; set; }

        private bool WasInMemoryTableLoaded { get; set; }

        public override void Execute(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Execute is not supported on CrossJoins! A crossjoin will continue execution"
                    + "when the predecessing dataflow components are completed"
            );
        }

        public CrossJoin()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CrossJoin([CanBeNull] ILogger<CrossJoin<TInput1, TInput2, TOutput>> logger)
            : base(logger)
        {
            InMemoryTarget = new MemoryDestination<TInput1>(this);
            PassingTarget = new CustomDestination<TInput2>(this, CrossJoinData)
            {
                OnCompletion = () => Buffer.Complete(),
            };
        }

        public CrossJoin(Func<TInput1, TInput2, TOutput> crossJoinFunc)
            : this()
        {
            CrossJoinFunc = crossJoinFunc;
        }

        private void CrossJoinData(TInput2 passingRow)
        {
            if (!WasInMemoryTableLoaded)
            {
                InMemoryTarget.Wait();
                WasInMemoryTableLoaded = true;
            }
            foreach (TInput1 inMemoryRow in InMemoryData)
            {
                CrossJoinAndSendRow(passingRow, inMemoryRow);
            }
        }

        private void CrossJoinAndSendRow(TInput2 passingRow, TInput1 inMemoryRow)
        {
            try
            {
                if (inMemoryRow is null || passingRow is null)
                {
                    return;
                }

                TOutput result = CrossJoinFunc.Invoke(inMemoryRow, passingRow);
                if (result is null)
                {
                    return;
                }

                Buffer.SendAsync(result).Wait();
                LogProgress();
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(
                    e,
                    string.Concat(
                        ErrorHandler.ConvertErrorData(inMemoryRow),
                        "  |--| ",
                        ErrorHandler.ConvertErrorData(passingRow)
                    )
                );
            }
        }
    }

    /// <summary>
    /// Will cross join data from the two inputs into one output. The input for the first table will be loaded into memory before the actual
    /// join can start. After this, every incoming row will be joined with every row of the InMemory-Table by the given function CrossJoinFunc.
    /// The InMemory target should always have the smaller amount of data to reduce memory consumption and processing time.
    /// </summary>
    /// <typeparam name="TInput">Type of data for both inputs and output.</typeparam>
    [PublicAPI]
    public class CrossJoin<TInput> : CrossJoin<TInput, TInput, TInput>
    {
        public CrossJoin() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CrossJoin(ILogger<CrossJoin<TInput>> logger)
            : base(logger) { }

        public CrossJoin(Func<TInput, TInput, TInput> crossJoinFunc)
            : base(crossJoinFunc) { }
    }

    /// <summary>
    /// Will cross join data from the two inputs into one output. The input for the first table will be loaded into memory before the actual
    /// join can start. After this, every incoming row will be joined with every row of the InMemory-Table by the given function CrossJoinFunc.
    /// The InMemory target should always have the smaller amount of data to reduce memory consumption and processing time.
    /// The non generic implementation deals with a dynamic object for both inputs and output.
    /// </summary>
    [PublicAPI]
    public class CrossJoin : CrossJoin<ExpandoObject, ExpandoObject, ExpandoObject>
    {
        public CrossJoin() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CrossJoin(ILogger<CrossJoin> logger)
            : base(logger) { }

        public CrossJoin(Func<ExpandoObject, ExpandoObject, ExpandoObject> crossJoinFunc)
            : base(crossJoinFunc) { }
    }
}
