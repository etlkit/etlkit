using System.Threading;

using EtlKit.Helper;
using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// A lookup task - data from the input can be enriched with data retrieved from the lookup source.
    /// </summary>
    /// <typeparam name="TInput">Type of data input and output</typeparam>
    /// <typeparam name="TSourceOutput">Type of lookup data</typeparam>
    [PublicAPI]
    public class LookupTransformation<TInput, TSourceOutput>
        : DataFlowTransformation<TInput, TInput>
    {
        /* ITask Interface */
        public override string TaskName { get; set; } = "Lookup";
        public List<TSourceOutput> LookupData { get; set; } = new();

        /* Public Properties */
        public override ISourceBlock<TInput> SourceBlock => RowTransformation.SourceBlock;
        public override ITargetBlock<TInput> TargetBlock => RowTransformation.TargetBlock;
        public IDataFlowSource<TSourceOutput> Source
        {
            get { return _source; }
            set
            {
                _source = value;
                Source.LinkTo(LookupBuffer);
            }
        }

        public Func<TInput, TInput> TransformationFunc
        {
            get { return _rowTransformationFunc; }
            set
            {
                _rowTransformationFunc = value;
                InitRowTransformation(LoadLookupData);
            }
        }

        /* Private stuff */
        private CustomDestination<TSourceOutput> LookupBuffer { get; set; }
        private RowTransformation<TInput, TInput> RowTransformation { get; set; }
        private Func<TInput, TInput> _rowTransformationFunc;
        private IDataFlowSource<TSourceOutput> _source;
        private LookupTypeInfo TypeInfo { get; set; }

        public LookupTransformation()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public LookupTransformation(
            [CanBeNull] ILogger<LookupTransformation<TInput, TSourceOutput>> logger
        )
            : base(logger)
        {
            LookupBuffer = new CustomDestination<TSourceOutput>(this, FillBuffer);
            DefaultInitWithMatchRetrieveAttributes();
        }

        public LookupTransformation(IDataFlowSource<TSourceOutput> lookupSource)
            : this()
        {
            Source = lookupSource;
        }

        public LookupTransformation(
            IDataFlowSource<TSourceOutput> lookupSource,
            Func<TInput, TInput> transformationFunc
        )
            : this(lookupSource)
        {
            TransformationFunc = transformationFunc;
        }

        public LookupTransformation(
            IDataFlowSource<TSourceOutput> lookupSource,
            Func<TInput, TInput> transformationFunc,
            List<TSourceOutput> lookupList
        )
            : this(lookupSource, transformationFunc)
        {
            LookupData = lookupList;
        }

        private void InitRowTransformation(Action initAction)
        {
            RowTransformation = new RowTransformation<TInput, TInput>(this, _rowTransformationFunc)
            {
                InitAction = initAction,
            };
        }

        private void DefaultInitWithMatchRetrieveAttributes()
        {
            _rowTransformationFunc = FindRowByAttributes;
            InitRowTransformation(() =>
            {
                ReadAndCheckTypeInfo();
                LoadLookupData();
            });
        }

        private void ReadAndCheckTypeInfo()
        {
            TypeInfo = new LookupTypeInfo(typeof(TInput), typeof(TSourceOutput));
            if (TypeInfo.MatchColumns.Count == 0 || TypeInfo.RetrieveColumns.Count == 0)
                throw new EtlKitException(
                    "Please define either a transformation function or use the MatchColumn / RetrieveColumn attributes."
                );
        }

        private TInput FindRowByAttributes(TInput row)
        {
            var lookupHit = LookupData.Find(e =>
            {
                var same = true;
                foreach (var mc in TypeInfo.MatchColumns)
                {
                    same &= mc.PropInInput.GetValue(row).Equals(mc.PropInOutput.GetValue(e));
                    if (!same)
                        break;
                }
                return same;
            });
            if (lookupHit is null)
            {
                return row;
            }

            foreach (var rc in TypeInfo.RetrieveColumns)
            {
                var retrieveValue = rc.PropInOutput.GetValue(lookupHit);
                rc.PropInInput.TrySetValue(row, retrieveValue);
            }
            return row;
        }

        private void LoadLookupData()
        {
            CheckLookupObjects();
            Source.Execute(CancellationToken.None);
            LookupBuffer.Wait();
        }

        private void CheckLookupObjects()
        {
            if (Source == null)
                throw new EtlKitException(
                    "You need to define a lookup source before using a LookupTransformation in a data flow"
                );
        }

        private void FillBuffer(TSourceOutput sourceRow)
        {
            LookupData ??= new List<TSourceOutput>();
            LookupData.Add(sourceRow);
        }

        public void LinkLookupSourceErrorTo(IDataFlowLinkTarget<EtlKitError> target) =>
            Source.LinkErrorTo(target);

        public void LinkLookupTransformationErrorTo(IDataFlowLinkTarget<EtlKitError> target) =>
            RowTransformation.LinkErrorTo(target);
    }

    /// <summary>
    /// A lookup task - data from the input can be enriched with data retrieved from the lookup source.
    /// The non generic implementation uses a dynamic object as input and lookup source.
    /// </summary>
    [PublicAPI]
    public class LookupTransformation : LookupTransformation<ExpandoObject, ExpandoObject>
    {
        public LookupTransformation() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public LookupTransformation(ILogger<LookupTransformation> logger)
            : base(logger) { }

        public LookupTransformation(IDataFlowSource<ExpandoObject> lookupSource)
            : base(lookupSource) { }

        public LookupTransformation(
            IDataFlowSource<ExpandoObject> lookupSource,
            Func<ExpandoObject, ExpandoObject> transformationFunc
        )
            : base(lookupSource, transformationFunc) { }

        public LookupTransformation(
            IDataFlowSource<ExpandoObject> lookupSource,
            Func<ExpandoObject, ExpandoObject> transformationFunc,
            List<ExpandoObject> lookupList
        )
            : base(lookupSource, transformationFunc, lookupList) { }
    }
}
