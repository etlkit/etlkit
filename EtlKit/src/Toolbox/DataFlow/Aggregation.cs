using System.Linq;

using EtlKit.Common;
using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Aggregates data by the given aggregation method.
    /// </summary>
    /// <remarks>
    /// There are three ways to initialize the aggregation:
    /// 1. Use the <see cref="AggregationAction"/> and <see cref="GroupingFunc"/> to define the aggregation logic.
    /// 2. Use the <see cref="Aggregation.Mappings"/> to define the aggregation logic.
    /// 3. Use the <see cref="GroupColumnAttribute"/> and <see cref="AggregateColumnAttribute"/> in the output to define
    ///    the aggregation logic.
    /// </remarks>
    /// <typeparam name="TInput">Type of data input</typeparam>
    /// <typeparam name="TOutput">Type of data output</typeparam>
    [PublicAPI]
    public class Aggregation<TInput, TOutput> : DataFlowTransformation<TInput, TOutput>
    {
        /* ITask Interface */
        public override string TaskName { get; set; } = "Execute aggregation block.";

        /* Public Properties */
        /// <summary>
        /// Aggregation function (optional)
        /// </summary>
        public Action<TInput, TOutput> AggregationAction
        {
            get { return _aggregationAction; }
            set
            {
                _aggregationAction = value;
                InputBuffer = new ActionBlock<TInput>(WrapAggregationAction);
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

        /// <summary>
        /// Grouping function (optional)
        /// </summary>
        public Func<TInput, object> GroupingFunc { get; set; }

        /// <summary>
        /// Store key action (optional).
        /// Finalizes the output row before writing it into the output buffer.
        /// Rewrites values in the grouping columns of output row object by
        /// their respective keys as defined by the <see cref="GroupingFunc"/>.
        /// </summary>
        public Action<object, TOutput> StoreKeyAction { get; set; }

        public override ISourceBlock<TOutput> SourceBlock => OutputBuffer;

        public override ITargetBlock<TInput> TargetBlock => InputBuffer;

        /* Private stuff */

        private BufferBlock<TOutput> OutputBuffer { get; set; }

        private ActionBlock<TInput> InputBuffer { get; set; }

        private Action<TInput, TOutput> _aggregationAction;

        private Dictionary<object, TOutput> AggregationData { get; set; } = new();

        protected IAggregationTypeInfo<TInput, TOutput> AggTypeInfo { get; set; }

        private void CheckTypeInfo()
        {
            if (AggTypeInfo.IsArrayOutput)
                throw new EtlKitException(
                    "Aggregation target must be of an object or dynamic type! Array types are not allowed."
                );
        }

        public Aggregation()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public Aggregation([CanBeNull] ILogger<Aggregation<TInput, TOutput>> logger)
            : base(logger)
        {
            OutputBuffer = new BufferBlock<TOutput>();
            AggTypeInfo = new AggregationTypeInfo<TInput, TOutput>();

            CheckTypeInfo();

            InitAggregationAction();
        }

        public Aggregation(Action<TInput, TOutput> aggregationAction)
            : this()
        {
            AggregationAction = aggregationAction;
        }

        public Aggregation(
            Action<TInput, TOutput> aggregationAction,
            Func<TInput, object> groupingFunc
        )
            : this(aggregationAction)
        {
            GroupingFunc = groupingFunc;
        }

        public Aggregation(
            Action<TInput, TOutput> aggregationAction,
            Func<TInput, object> groupingFunc,
            Action<object, TOutput> storeKeyAction
        )
            : this(aggregationAction, groupingFunc)
        {
            StoreKeyAction = storeKeyAction;
        }

        protected void InitAggregationAction()
        {
            if (AggregationAction == null && AggTypeInfo.AggregateColumns.Count > 0)
                AggregationAction = DefineAggregationAction;

            if (GroupingFunc == null && AggTypeInfo.GroupColumns.Count > 0)
                GroupingFunc = DefineGroupingFunc;

            if (StoreKeyAction == null && AggTypeInfo.GroupColumns.Count > 0)
                StoreKeyAction = DefineStoreKeyAction;
        }

        private void DefineAggregationAction(TInput inputRow, TOutput aggOutput)
        {
            foreach (var attributeMapping in AggTypeInfo.AggregateColumns)
            {
                var inputVal = AggTypeInfo.GetInputValue(inputRow, attributeMapping);
                var aggVal = AggTypeInfo.GetOutputValueOrNull(aggOutput, attributeMapping);

                var res = (aggVal, attributeMapping.AggregationMethod) switch
                {
                    (null, AggregationMethod.Count) => 1,
                    (null, _) => inputVal,
                    (_, AggregationMethod.Sum)
                        when aggVal is int or uint or decimal or long or double or float =>
                        Convert.ToDecimal(inputVal ?? 0) + Convert.ToDecimal(aggVal),
                    (_, AggregationMethod.Max) when aggVal is IComparable => (
                        Convert.ChangeType(inputVal, aggVal.GetType()) as IComparable
                    )?.CompareTo(aggVal) > 0
                        ? inputVal
                        : aggVal,
                    (_, AggregationMethod.Min) when aggVal is IComparable => (
                        Convert.ChangeType(inputVal, aggVal.GetType()) as IComparable
                    )?.CompareTo(aggVal) < 0
                        ? inputVal
                        : aggVal,
                    (_, AggregationMethod.Count)
                        when aggVal is int or uint or decimal or long or double or float =>
                        Convert.ToInt64(aggVal) + 1,
                    (_, _) => null,
                };

                AggTypeInfo.SetOutputValueOrThrow(aggOutput, res, attributeMapping, true);
            }
        }

        private void DefineStoreKeyAction(object key, TOutput outputRow)
        {
            if (key is not GroupingKey gk)
                return;
            foreach (var groupingProperty in gk.GroupingObjectsByProperty)
            {
                AttributeMappingInfo attributeMapping = groupingProperty.Key;
                AggTypeInfo.SetOutputValueOrThrow(
                    outputRow,
                    groupingProperty.Value,
                    attributeMapping,
                    false
                );
            }
        }

        private GroupingKey DefineGroupingFunc(TInput inputRow)
        {
            var groupingKey = new GroupingKey();
            foreach (var propMap in AggTypeInfo.GroupColumns)
                groupingKey.GroupingObjectsByProperty.Add(
                    propMap,
                    AggTypeInfo.GetInputValue(inputRow, propMap)
                );
            return groupingKey;
        }

        private void WriteIntoOutput()
        {
            LogStart();
            foreach (var row in AggregationData)
            {
                StoreKeyAction?.Invoke(row.Key, row.Value);
                OutputBuffer.SendAsync(row.Value).Wait();
                LogProgress();
            }
            LogFinish();
        }

        protected virtual void WrapAggregationAction(TInput row)
        {
            var key = GroupingFunc?.Invoke(row) ?? string.Empty;

            if (!AggregationData.ContainsKey(key))
                AddRecordToDict(key);

            TOutput currentAgg = AggregationData[key];
            AggregationAction.Invoke(row, currentAgg);
        }

        private void AddRecordToDict(object key)
        {
            var firstEntry = (TOutput)Activator.CreateInstance(typeof(TOutput));
            AggregationData.Add(key, firstEntry);
        }

        private sealed class GroupingKey
        {
            public Dictionary<AttributeMappingInfo, object> GroupingObjectsByProperty { get; } =
                new();

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 29;
                    foreach (var map in GroupingObjectsByProperty)
                        hash = hash * 486187739 + (map.Value?.GetHashCode() ?? 17);
                    return hash;
                }
            }

            public override bool Equals(object obj) =>
                obj is GroupingKey comp
                && GroupingObjectsByProperty.All(map =>
                    map.Value?.Equals(comp.GroupingObjectsByProperty[map.Key]) ?? true
                );
        }
    }

    public enum AggregationMethod
    {
        Sum,
        Min,
        Max,
        Count,
    }

    /// <summary>
    /// Aggregates data by the given aggregation method.
    /// The non generic implementation uses dynamic objects.
    /// </summary>
    /// <see cref="Aggregation{TInput, TOutput}"/>
    [PublicAPI]
    public class Aggregation : Aggregation<ExpandoObject, ExpandoObject>
    {
        /// <summary>
        /// Mappings in the form of a dictionary for use with serialization.
        /// The key is the name of the property in the output object,
        /// the value is the reference to the property in the input object and the aggregation function to use.
        /// </summary>
        public Dictionary<string, InputAggregationField> Mappings
        {
            get => _mappings;
            set
            {
                _mappings = value;
                AggTypeInfo = new DynamicAggregationTypeInfo(_mappings);
                InitAggregationAction();
            }
        }

        private Dictionary<string, InputAggregationField> _mappings;

        public Aggregation() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public Aggregation(ILogger<Aggregation> logger)
            : base(logger) { }

        public Aggregation(Action<ExpandoObject, ExpandoObject> aggregationAction)
            : base(aggregationAction) { }

        public Aggregation(
            Action<ExpandoObject, ExpandoObject> aggregationAction,
            Func<ExpandoObject, object> groupingFunc
        )
            : base(aggregationAction, groupingFunc) { }

        public Aggregation(
            Action<ExpandoObject, ExpandoObject> aggregationAction,
            Func<ExpandoObject, object> groupingFunc,
            Action<object, ExpandoObject> storeKeyAction
        )
            : base(aggregationAction, groupingFunc, storeKeyAction) { }
    }
}
