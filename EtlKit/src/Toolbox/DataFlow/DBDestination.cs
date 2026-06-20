using System.Linq;

using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;

using Microsoft.Extensions.Logging;

using DataFlow_TypeInfo = EtlKit.Common.DataFlow.TypeInfo;
using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// A database destination represents a table where data from the flow is inserted.
    /// Inserts are done in batches (using Bulk insert or an equivalent).
    /// </summary>
    /// <see cref="DbDestination"/>
    /// <typeparam name="TInput">Data type for input, preferably representing the destination table.</typeparam>
    [PublicAPI]
    public class DbDestination<TInput> : DataFlowBatchDestination<TInput>
    {
        /* ITask Interface */
        public override string TaskName =>
            $"Write data into table {DestinationTableDefinition?.Name ?? TableName}";

        /* Public properties */
        /// <summary>
        /// If you don't want ETLBox to dynamically read the destination table definition from the database,
        /// you can provide your own table definition.
        /// </summary>
        public TableDefinition DestinationTableDefinition { get; set; }

        /// <summary>
        /// Name of the target table that receives the data from the data flow.
        /// </summary>
        public string TableName { get; set; }

        /* Private stuff */
        private DataFlow_TypeInfo TypeInfo { get; set; }
        private bool HasDestinationTableDefinition => DestinationTableDefinition != null;
        private bool HasTableName => !string.IsNullOrWhiteSpace(TableName);

        [CanBeNull]
        private TableData<TInput> TableData { get; set; }

        [CanBeNull]
        internal IConnectionManager BulkInsertConnectionManager =>
            _ownBulkInsertConnectionManager ?? DbConnectionManager;

        [CanBeNull]
        private IConnectionManager _ownBulkInsertConnectionManager;

        public DbDestination()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public DbDestination(ILogger<DbDestination<TInput>> logger)
            : base(logger)
        {
            // this internally calls InitObjects()
            BatchSize = DefaultBatchSize;
        }

        public DbDestination(int batchSize)
        {
            BatchSize = batchSize;
        }

        public DbDestination(string tableName)
            : this()
        {
            TableName = tableName;
        }

        public DbDestination(IConnectionManager connectionManager, string tableName)
            : this(tableName)
        {
            ConnectionManager = connectionManager;
        }

        public DbDestination(string tableName, int batchSize)
            : this(batchSize)
        {
            TableName = tableName;
        }

        public DbDestination(IConnectionManager connectionManager, string tableName, int batchSize)
            : this(tableName, batchSize)
        {
            ConnectionManager = connectionManager;
        }

        protected override void InitObjects(int initBatchSize)
        {
            base.InitObjects(initBatchSize);
            TypeInfo = new DataFlow_TypeInfo(typeof(TInput)).GatherTypeInfo();
        }

        private void CloneIfAllowed()
        {
            if (DbConnectionManager.LeaveOpen)
            {
                return;
            }

            _ownBulkInsertConnectionManager?.Dispose();
            _ownBulkInsertConnectionManager = DbConnectionManager.Clone();
        }

        private void LoadTableDefinitionFromTableName()
        {
            if (HasTableName)
                DestinationTableDefinition = TableDefinition.GetDefinitionFromTableName(
                    DbConnectionManager,
                    TableName
                );
            else if (!HasDestinationTableDefinition && !HasTableName)
                throw new EtlKitException(
                    "No Table definition or table name found! You must provide a table name or a table definition."
                );
        }

        protected override void PrepareWrite()
        {
            if (!HasDestinationTableDefinition)
                LoadTableDefinitionFromTableName();

            TableData ??= new TableData<TInput>(DestinationTableDefinition, BatchSize);

            CloneIfAllowed();
            BulkInsertConnectionManager!.PrepareBulkInsert(DestinationTableDefinition.Name);
        }

        protected override void TryBulkInsertData(TInput[] data)
        {
            if (TableData is null)
            {
                throw new EtlKitException("TableData is null.");
            }

            TryAddDynamicColumnsToTableDef(data);
            try
            {
                TableData.ClearData();
                ConvertAndAddRows(data);
                var sql = new SqlTask(this, "Execute Bulk insert")
                {
                    DisableLogging = true,
                    ConnectionManager = BulkInsertConnectionManager,
                };
                sql.BulkInsert(TableData, DestinationTableDefinition.Name);
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(data));
            }
        }

        protected override void FinishWrite()
        {
            TableData?.Close();
            TableData = null;

            BulkInsertConnectionManager?.CleanUpBulkInsert(DestinationTableDefinition?.Name);

            _ownBulkInsertConnectionManager?.Dispose(); // initialized when LeaveOpen == false
            _ownBulkInsertConnectionManager = null;
        }

        private void TryAddDynamicColumnsToTableDef(TInput[] data)
        {
            if (!TypeInfo.IsDynamic || data.Length <= 0)
            {
                return;
            }

            foreach (
                var dynamicObject in data.Select(x => x as IDictionary<string, object>)
                    .Where(x => x != null)
            )
            {
                foreach (var key in dynamicObject.Select(c => c.Key))
                {
                    var newPropIndex = TableData!.DynamicColumnNames.Count;
                    if (!TableData.DynamicColumnNames.ContainsKey(key))
                        TableData.DynamicColumnNames.Add(key, newPropIndex);
                }
            }
        }

        private void ConvertAndAddRows(TInput[] data)
        {
            foreach (var currentRow in data)
            {
                if (currentRow is null)
                    continue;

                TableData!.Rows.Add(
                    TypeInfo.GetTypeInfoGroup() switch
                    {
                        DataFlow_TypeInfo.TypeInfoGroup.Array => currentRow as object[],
                        DataFlow_TypeInfo.TypeInfoGroup.Dynamic => ConvertDynamicRow(
                            currentRow as IDictionary<string, object>
                        ),
                        _ => ConvertObjectRow(currentRow),
                    }
                );
            }
        }

        private object[] ConvertObjectRow(TInput currentRow)
        {
            var rowResult = new object[TypeInfo.PropertyLength];
            var index = 0;
            foreach (PropertyInfo propInfo in TypeInfo.Properties)
            {
                rowResult[index] = propInfo.GetValue(currentRow);
                index++;
            }

            return rowResult;
        }

        private object[] ConvertDynamicRow(IDictionary<string, object> propertyValues)
        {
            var rowResult = new object[TableData!.DynamicColumnNames.Count];
            foreach (var prop in propertyValues)
            {
                var columnIndex = TableData.DynamicColumnNames[prop.Key];
                rowResult[columnIndex] = prop.Value;
            }

            return rowResult;
        }
    }

    /// <summary>
    /// A database destination represents a table where data from the flow is inserted.
    /// Inserts are done in batches (using Bulk insert or an equivalent).
    /// The DbDestination uses the dynamic ExpandoObject as input type.
    /// If you need other data types, use the generic DbDestination instead.
    /// </summary>
    /// <see cref="DbDestination{TInput}"/>
    [PublicAPI]
    public class DbDestination : DbDestination<ExpandoObject>
    {
        public DbDestination() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public DbDestination(ILogger<DbDestination> logger)
            : base(logger) { }

        public DbDestination(int batchSize)
            : base(batchSize) { }

        public DbDestination(string tableName)
            : base(tableName) { }

        public DbDestination(IConnectionManager connectionManager, string tableName)
            : base(connectionManager, tableName) { }

        public DbDestination(string tableName, int batchSize)
            : base(tableName, batchSize) { }

        public DbDestination(IConnectionManager connectionManager, string tableName, int batchSize)
            : base(connectionManager, tableName, batchSize) { }
    }
}
