using EtlKit.Primitives;

using EtlKit.Common.ControlFlow;
using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Will join data from the two inputs into one output - on a row by row base. Make sure both inputs are sorted or in the right order.
    /// </summary>
    /// <typeparam name="TInput1">Type of data for input block one.</typeparam>
    /// <typeparam name="TInput2">Type of data for input block two.</typeparam>
    /// <typeparam name="TOutput">Type of output data.</typeparam>
    /// <example>
    /// <code>
    /// MergeJoin&lt;MyDataRow1, MyDataRow2, MyDataRow1&gt; join = new MergeJoin&lt;MyDataRow1, MyDataRow2, MyDataRow1&gt;(Func&lt;TInput1, TInput2, TOutput&gt; mergeJoinFunc);
    /// source1.LinkTo(join.Target1);;
    /// source2.LinkTo(join.Target2);;
    /// join.LinkTo(dest);
    /// </code>
    /// </example>
    [PublicAPI]
    public class MergeJoin<TInput1, TInput2, TOutput> : DataFlowTask, IDataFlowLinkSource<TOutput>
    {
        private Func<TInput1, TInput2, TOutput> _mergeJoinFunc;

        /* ITask Interface */
        public sealed override string TaskName { get; set; } = "Merge and join data";

        /* Public Properties */
        public MergeJoinTarget<TInput1> Target1 { get; set; }
        public MergeJoinTarget<TInput2> Target2 { get; set; }
        public ISourceBlock<TOutput> SourceBlock => Transformation.SourceBlock;

        public Func<TInput1, TInput2, TOutput> MergeJoinFunc
        {
            get { return _mergeJoinFunc; }
            set
            {
                _mergeJoinFunc = value;
                Transformation.TransformationFunc = tuple =>
                    _mergeJoinFunc.Invoke(tuple.Item1, tuple.Item2);
                JoinBlock.LinkTo(
                    Transformation.TargetBlock,
                    new DataflowLinkOptions { PropagateCompletion = true }
                );
            }
        }

        /* Private stuff */
        internal JoinBlock<TInput1, TInput2> JoinBlock { get; set; }
        internal RowTransformation<Tuple<TInput1, TInput2>, TOutput> Transformation { get; set; }

        public MergeJoin()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public MergeJoin([CanBeNull] ILogger<MergeJoin<TInput1, TInput2, TOutput>> logger)
            : base(logger)
        {
            Transformation = new RowTransformation<Tuple<TInput1, TInput2>, TOutput>(this);
            JoinBlock = new JoinBlock<TInput1, TInput2>();
            Target1 = new MergeJoinTarget<TInput1>(this, JoinBlock.Target1);
            Target2 = new MergeJoinTarget<TInput2>(this, JoinBlock.Target2);
        }

        public MergeJoin(Func<TInput1, TInput2, TOutput> mergeJoinFunc)
            : this()
        {
            MergeJoinFunc = mergeJoinFunc;
        }

        public MergeJoin(string name, Func<TInput1, TInput2, TOutput> mergeJoinFunc)
            : this(mergeJoinFunc)
        {
            TaskName = name;
        }

        public IDataFlowLinkSource<TOutput> LinkTo(IDataFlowLinkTarget<TOutput> target) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target);

        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target, predicate);

        public IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo(target, rowsToKeep, rowsIntoVoid);

        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(target);

        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        ) => new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(target, predicate);

        public IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        ) =>
            new DataFlowLinker<TOutput>(this, SourceBlock).LinkTo<TConvert>(
                target,
                rowsToKeep,
                rowsIntoVoid
            );

        public void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target) =>
            Transformation.LinkErrorTo(target);
    }

    [PublicAPI]
    public class MergeJoinTarget<TInput> : GenericTask, IDataFlowDestination<TInput>
    {
        public ITargetBlock<TInput> TargetBlock { get; set; }

        public void Wait()
        {
            TargetBlock.Completion.Wait();
        }

        public Task Completion => TargetBlock.Completion;

        private List<Task> PredecessorCompletions { get; set; } = new();

        public void AddPredecessorCompletion(Task completion)
        {
            PredecessorCompletions.Add(completion);
            completion.ContinueWith(_ => CheckCompleteAction());
        }

        private void CheckCompleteAction()
        {
            Task.WhenAll(PredecessorCompletions)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        TargetBlock.Fault(t.Exception!.InnerException!);
                    else
                        TargetBlock.Complete();
                });
        }

        public MergeJoinTarget(ITask parent, ITargetBlock<TInput> joinTarget)
        {
            TargetBlock = joinTarget;
            CopyTaskProperties(parent);
        }
    }

    /// <summary>
    /// Will join data from the two inputs into one output - on a row by row base. Make sure both inputs are sorted or in the right order.
    /// </summary>
    /// <typeparam name="TInput">Type of data for both inputs and output.</typeparam>
    /// <example>
    /// <code>
    /// MergeJoin&lt;MyDataRow&gt; join = new MergeJoin&lt;MyDataRow&gt;(mergeJoinFunc);
    /// source1.LinkTo(join.Target1);;
    /// source2.LinkTo(join.Target2);;
    /// join.LinkTo(dest);
    /// </code>
    /// </example>
    [PublicAPI]
    public class MergeJoin<TInput> : MergeJoin<TInput, TInput, TInput>
    {
        public MergeJoin() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public MergeJoin(ILogger<MergeJoin<TInput>> logger)
            : base(logger) { }

        public MergeJoin(Func<TInput, TInput, TInput> mergeJoinFunc)
            : base(mergeJoinFunc) { }

        public MergeJoin(string name, Func<TInput, TInput, TInput> mergeJoinFunc)
            : base(name, mergeJoinFunc) { }
    }

    /// <summary>
    /// Will join data from the two inputs into one output - on a row by row base.
    /// Make sure both inputs are sorted or in the right order. The non generic implementation deals with
    /// a dynamic object as input and merged output.
    /// </summary>
    [PublicAPI]
    public class MergeJoin : MergeJoin<ExpandoObject, ExpandoObject, ExpandoObject>
    {
        public MergeJoin() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public MergeJoin(ILogger<MergeJoin> logger)
            : base(logger) { }

        public MergeJoin(Func<ExpandoObject, ExpandoObject, ExpandoObject> mergeJoinFunc)
            : base(mergeJoinFunc) { }

        public MergeJoin(
            string name,
            Func<ExpandoObject, ExpandoObject, ExpandoObject> mergeJoinFunc
        )
            : base(name, mergeJoinFunc) { }
    }
}
