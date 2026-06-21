using System.Linq;
using System.Text;
using System.Threading;
using EtlKit.Common;
using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;
using EtlKit.Helper;
using EtlKit.Primitives;
using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Inserts, updates and (optionally) deletes data in db target.
    /// </summary>
    /// <typeparam name="TInput">Type of input data.</typeparam>
    /// <example>
    /// <code>
    /// </code>
    /// </example>
    [PublicAPI]
    public class DbMerge<TInput>
        : DataFlowTransformation<TInput, TInput>,
            IDataFlowBatchDestination<TInput>
    {
        /* ITask Interface */
        public override string TaskName { get; set; } = "Insert, update or delete in destination";

        public async Task ExecuteAsync() =>
            await OutputSource.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        public void Execute() => OutputSource.Execute(CancellationToken.None);

        /* Public Properties */
        public override ISourceBlock<TInput> SourceBlock => OutputSource.SourceBlock;
        public override ITargetBlock<TInput> TargetBlock => Lookup.TargetBlock;
        public DeltaMode DeltaMode { get; set; }
        public TableDefinition DestinationTableDefinition { get; set; }
        public string TableName
        {
            get => _tableName;
            set
            {
                _tableName = value;
                DestinationTableAsSource = new DbSource<TInput>(ConnectionManager, TableName);
                DestinationTable = new DbDestination<TInput>(
                    ConnectionManager,
                    TableName,
                    BatchSize
                );
                InitInternalFlow();
                InitOutputFlow();
            }
        }

        public List<TInput> DeltaTable { get; set; } = new();
        public bool UseTruncateMethod
        {
            get
            {
                if (IdColumnNames == null || IdColumnNames?.Count == 0)
                    return true;
                return _useTruncateMethod;
            }
            set { _useTruncateMethod = value; }
        }

        private bool _useTruncateMethod;

        private int _batchSize = DbDestination.DefaultBatchSize;

        public int BatchSize
        {
            get => _batchSize;
            set
            {
                _batchSize = value;
                if (DestinationTable != null)
                    DestinationTable.BatchSize = value;
            }
        }

        public MergeProperties MergeProperties { get; set; } = new();

        /* Private stuff */
        private List<string> IdColumnNames =>
            MergeProperties.IdPropertyNames?.Count > 0
                ? MergeProperties.IdPropertyNames
                : TypeInfo?.IdColumnNames;

        private ObjectNameDescriptor TN => new(TableName, QB, QE);
        private LookupTransformation<TInput, TInput> Lookup { get; set; }
        private DbSource<TInput> DestinationTableAsSource { get; set; }
        private DbDestination<TInput> DestinationTable { get; set; }
        private List<TInput> InputData => Lookup.LookupData;
        private Dictionary<string, TInput> InputDataDict { get; set; }
        private CustomSource<TInput> OutputSource { get; set; }
        private bool WasTruncationExecuted { get; set; }
        private DBMergeTypeInfo TypeInfo { get; set; }

        public DbMerge()
            : this(null, null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public DbMerge(ILogger<DbMerge<TInput>> logger)
            : base(logger) { }

        public DbMerge(string tableName)
            : this(null, tableName) { }

        public DbMerge(
            IConnectionManager connectionManager,
            string tableName,
            int batchSize = DbDestination.DefaultBatchSize
        )
        {
            BatchSize = batchSize;
            if (connectionManager != null)
                ConnectionManager = connectionManager;
            if (!string.IsNullOrEmpty(tableName))
                TableName = tableName;
        }

        protected sealed override void OnConnectionManagerChanged(IConnectionManager value)
        {
            if (DestinationTableAsSource is not null)
                DestinationTableAsSource.ConnectionManager = value;
            if (DestinationTable is not null)
                DestinationTable.ConnectionManager = value;
            base.OnConnectionManagerChanged(value);
        }

        public ChangeAction? GetChangeAction(TInput row)
        {
            if (TypeInfo.IsDynamic)
            {
                var r = row as IDictionary<string, object>;
                if (!r!.ContainsKey("ChangeAction"))
                    r.Add("ChangeAction", null as ChangeAction?);
                return r["ChangeAction"] as ChangeAction?;
            }

            if (TypeInfo.ChangeActionProperty != null)
            {
                return TypeInfo.ChangeActionProperty.GetValue(row) as ChangeAction?;
            }

            throw new EtlKitNotSupportedException(
                "Objects used for merge must inherit from MergeableRow or"
                    + "contain a property ChangeAction (public ChangeAction? ChangeAction {get;set;}"
            );
        }

        public void SetChangeAction(TInput row, ChangeAction? changeAction)
        {
            if (TypeInfo.IsDynamic)
            {
                dynamic r = row as ExpandoObject;
                r!.ChangeAction = changeAction;
            }
            else if (TypeInfo.ChangeActionProperty != null)
            {
                TypeInfo.ChangeActionProperty.SetValueOrThrow(row, changeAction);
            }
            else
                throw new EtlKitNotSupportedException(
                    "Objects used for merge must inherit from MergeableRow or"
                        + "contain a property ChangeAction (public ChangeAction? ChangeAction {get;set;}"
                );
        }

        public string GetUniqueId(TInput row)
        {
            StringBuilder resultBuilder = new();
            if (TypeInfo.IsDynamic && MergeProperties.IdPropertyNames.Count > 0)
            {
                var r = row as IDictionary<string, object>;
                foreach (var idColumn in MergeProperties.IdPropertyNames)
                {
                    if (!r!.ContainsKey(idColumn))
                        r.Add(idColumn, null);
                    resultBuilder.Append(r[idColumn]);
                }
                return resultBuilder.ToString();
            }

            if (TypeInfo.IdAttributeProps.Count <= 0)
            {
                throw new EtlKitNotSupportedException(
                    "Objects used for merge must at least define a id column"
                        + "to identify matching rows - please use the IdColumn attribute or add a property name in the MergeProperties.IdProperyNames list."
                );
            }

            foreach (var propInfo in TypeInfo.IdAttributeProps)
                resultBuilder.Append(propInfo?.GetValue(row));
            return resultBuilder.ToString();
        }

        public bool GetIsDeletion(TInput row)
        {
            if (TypeInfo.IsDynamic && MergeProperties.DeletionProperties.Count > 0)
            {
                return FindDeletionDynamicProperty(row as IDictionary<string, object>);
            }

            if (TypeInfo.DeleteAttributeProps.Count <= 0)
            {
                return false;
            }

            return TypeInfo.DeleteAttributeProps.TrueForAll(tuple =>
                tuple.Item1?.GetValue(row)?.Equals(tuple.Item2) ?? false
            );
        }

        private bool FindDeletionDynamicProperty(IDictionary<string, object> r)
        {
            var result = true;
            foreach (var deleteColumn in MergeProperties.DeletionProperties)
            {
                if (r!.TryGetValue(deleteColumn.Key, out var property))
                    result &= property?.Equals(deleteColumn.Value) ?? true;
                else
                    result = false;
            }

            return result;
        }

        public void SetChangeDate(TInput row, DateTime changeDate)
        {
            if (TypeInfo.IsDynamic)
            {
                dynamic r = row as ExpandoObject;
                r!.ChangeDate = changeDate;
            }
            else if (TypeInfo.ChangeDateProperty != null)
            {
                TypeInfo.ChangeDateProperty.SetValueOrThrow(row, changeDate);
            }
            else
                throw new EtlKitNotSupportedException(
                    "Objects used for merge must inherit from MergeableRow or "
                        + "contain a property ChangeDate (public DateTime ChangeDate {get;set;}"
                );
        }

        public bool AreEqual(object self, object other)
        {
            if (other == null || self == null)
                return false;
            if (TypeInfo.IsDynamic)
            {
                return CompareDynamicObjects(
                    self as IDictionary<string, object>,
                    other as IDictionary<string, object>
                );
            }

            return TypeInfo.CompareAttributeProps.Count > 0
                && TypeInfo.CompareAttributeProps.TrueForAll(propInfo =>
                    propInfo?.GetValue(self)?.Equals(propInfo.GetValue(other)) ?? false
                );
        }

        private bool CompareDynamicObjects(
            IDictionary<string, object> self,
            IDictionary<string, object> other
        ) =>
            MergeProperties.ComparePropertyNames.TrueForAll(compColumn =>
                self!.ContainsKey(compColumn)
                && other!.ContainsKey(compColumn)
                && self[compColumn]!.Equals(other[compColumn])
            );

        private void InitInternalFlow()
        {
            Lookup = new LookupTransformation<TInput, TInput>(
                DestinationTableAsSource,
                UpdateRowWithDeltaInfo
            );

            DestinationTable.BeforeBatchWrite = batch =>
            {
                DeltaTable.AddRange(
                    DeltaMode == DeltaMode.Delta
                        ? batch.Where(row => GetChangeAction(row) != ChangeAction.Delete)
                        : batch
                );

                if (!UseTruncateMethod)
                {
                    SqlDeleteIds(
                        batch.Where(row =>
                            GetChangeAction(row) != ChangeAction.Insert
                            && GetChangeAction(row) != ChangeAction.Exists
                        )
                    );
                    return batch
                        .Where(row =>
                            GetChangeAction(row) == ChangeAction.Insert
                            || GetChangeAction(row) == ChangeAction.Update
                        )
                        .ToArray();
                }

                if (DeltaMode == DeltaMode.Delta)
                    throw new EtlKitNotSupportedException(
                        "If you provide a delta load, you must define at least one compare column."
                            + "Using the truncate method is not allowed. "
                    );
                TruncateDestinationOnce();
                return batch
                    .Where(row =>
                        GetChangeAction(row) == ChangeAction.Insert
                        || GetChangeAction(row) == ChangeAction.Update
                        || GetChangeAction(row) == ChangeAction.Exists
                    )
                    .ToArray();
            };

            Lookup.LinkTo(DestinationTable);
        }

        private void InitOutputFlow()
        {
            var x = 0;
            OutputSource = new CustomSource<TInput>(
                () => DeltaTable[x++],
                () => x >= DeltaTable.Count
            );

            DestinationTable.OnCompletion = () =>
            {
                IdentifyAndDeleteMissingEntries();
                OutputSource.Execute(CancellationToken.None);
            };
        }

        private TInput UpdateRowWithDeltaInfo(TInput row)
        {
            if (!_wasTypeInfoInitialized)
                InitTypeInfo();
            if (InputDataDict == null)
                InitInputDataDictionary();
            SetChangeDate(row, DateTime.Now);
            InputDataDict!.TryGetValue(GetUniqueId(row), out TInput find);
            if (DeltaMode == DeltaMode.Delta && GetIsDeletion(row))
            {
                SetDeleteAction(row, find);
            }
            else
            {
                SetInsertUpdateAction(row, find);
            }
            return row;
        }

        private void SetInsertUpdateAction(TInput row, TInput find)
        {
            SetChangeAction(row, ChangeAction.Insert);
            if (find is null)
            {
                return;
            }

            if (AreEqual(row, find))
            {
                SetChangeAction(row, ChangeAction.Exists);
                SetChangeAction(find, ChangeAction.Exists);
            }
            else
            {
                SetChangeAction(row, ChangeAction.Update);
                SetChangeAction(find, ChangeAction.Update);
            }
        }

        private void SetDeleteAction(TInput row, TInput find)
        {
            if (find is null)
            {
                return;
            }

            SetChangeAction(find, ChangeAction.Delete);
            SetChangeAction(row, ChangeAction.Delete);
        }

        private void InitInputDataDictionary()
        {
            InputDataDict = new Dictionary<string, TInput>();
            foreach (var d in InputData)
                InputDataDict.Add(GetUniqueId(d), d);
        }

        private bool _wasTypeInfoInitialized;
        private string _tableName;

        private void InitTypeInfo()
        {
            TypeInfo = new DBMergeTypeInfo(typeof(TInput), MergeProperties);
            _wasTypeInfoInitialized = true;
        }

        private void TruncateDestinationOnce()
        {
            if (WasTruncationExecuted)
                return;
            WasTruncationExecuted = true;
            if (DeltaMode == DeltaMode.NoDeletions)
                return;
            TruncateTableTask.Truncate(ConnectionManager, TableName);
        }

        private void IdentifyAndDeleteMissingEntries()
        {
            if (DeltaMode == DeltaMode.NoDeletions)
                return;
            IEnumerable<TInput> deletions;
            if (DeltaMode == DeltaMode.Delta)
                deletions = InputData
                    .Where(row => GetChangeAction(row) == ChangeAction.Delete)
                    .ToList();
            else
                deletions = InputData.Where(row => GetChangeAction(row) == null).ToList();
            if (!UseTruncateMethod)
                SqlDeleteIds(deletions);
            foreach (var row in deletions) //.ForEach(row =>
            {
                SetChangeAction(row, ChangeAction.Delete);
                SetChangeDate(row, DateTime.Now);
            }
            DeltaTable.AddRange(deletions);
        }

        private void SqlDeleteIds(IEnumerable<TInput> rowsToDelete)
        {
            var delete = rowsToDelete as TInput[] ?? rowsToDelete?.ToArray();
            if (delete is null or { Length: 0 })
                return;
            var deleteString = delete.Select(row => $"'{GetUniqueId(row)}'");
            var idNames = $"{QB}{IdColumnNames[0]}{QE}";
            if (IdColumnNames.Count > 1)
                idNames = CreateConcatSqlForNames();
            new SqlTask(
                this,
                $@"
            DELETE FROM {TN.QuotedFullName} 
            WHERE {idNames} IN (
            {string.Join(",", deleteString)}
            )"
            )
            {
                DisableLogging = true,
            }.ExecuteNonQuery();
        }

        private string CreateConcatSqlForNames()
        {
            var result =
                $"CONCAT( {string.Join(",", IdColumnNames.Select(cn => $"{QB}{cn}{QE}"))} )";
            if (ConnectionType == ConnectionManagerType.SQLite)
                result = $" {string.Join("||", IdColumnNames.Select(cn => $"{QB}{cn}{QE}"))} ";
            return result;
        }

        public void Wait() => DestinationTable.Wait();

        public Task Completion => DestinationTable.Completion;
    }

    public enum DeltaMode
    {
        Full = 0,
        NoDeletions = 1,
        Delta = 2,
    }

    public class MergeProperties
    {
        public List<string> IdPropertyNames { get; set; } = new();
        public List<string> ComparePropertyNames { get; set; } = new();
        public Dictionary<string, object> DeletionProperties { get; set; } = new();
        internal string ChangeActionPropertyName { get; set; } = "ChangeAction";
        internal string ChangeDatePropertyName { get; set; } = "ChangeDate";
    }

    /// <inheritdoc/>
    [PublicAPI]
    public class DbMerge : DbMerge<ExpandoObject>
    {
        // for deserialization purposes
        public DbMerge() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public DbMerge(ILogger<DbMerge> logger)
            : base(logger) { }

        public DbMerge(string tableName)
            : base(tableName) { }

        public DbMerge(IConnectionManager connectionManager, string tableName)
            : base(connectionManager, tableName) { }

        public DbMerge(string tableName, int batchSize)
            : base(null, tableName, batchSize) { }

        public DbMerge(IConnectionManager connectionManager, string tableName, int batchSize)
            : base(connectionManager, tableName, batchSize) { }
    }
}
