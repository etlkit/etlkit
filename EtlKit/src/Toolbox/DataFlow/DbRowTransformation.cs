using System.Linq;

using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;

using DataFlow_TypeInfo = EtlKit.Common.DataFlow.TypeInfo;
using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow;

[PublicAPI]
public class DbRowTransformation<TInput> : RowTransformation<TInput>
{
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
    private IConnectionManager BulkInsertConnectionManager =>
        _ownBulkInsertConnectionManager ?? DbConnectionManager;

    [CanBeNull]
    private IConnectionManager _ownBulkInsertConnectionManager;

    public DbRowTransformation()
    {
        InitObjects();
        TransformationFunc = source =>
        {
            PrepareWrite();
            try
            {
                return TryBulkInsertData(source) ? source : default;
            }
            finally
            {
                FinishWrite();
            }
        };
    }

    public DbRowTransformation(string tableName)
        : this()
    {
        TableName = tableName;
        InitObjects();
    }

    public DbRowTransformation(IConnectionManager connectionManager, string tableName)
        : this(tableName)
    {
        ConnectionManager = connectionManager;
    }

    private void InitObjects()
    {
        TypeInfo = new DataFlow_TypeInfo(typeof(TInput)).GatherTypeInfo();
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

    private void PrepareWrite()
    {
        if (!HasDestinationTableDefinition)
            LoadTableDefinitionFromTableName();

        CloneIfAllowed();
        BulkInsertConnectionManager!.PrepareBulkInsert(DestinationTableDefinition.Name);
        TableData ??= new TableData<TInput>(DestinationTableDefinition, 1);
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

    private bool TryBulkInsertData(params TInput[] data)
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
            return true;
        }
        catch (Exception e)
        {
            if (!ErrorHandler.HasErrorBuffer)
                throw;
            ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(data));
            return false;
        }
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

    private void FinishWrite()
    {
        TableData?.Close();
        TableData = null;

        BulkInsertConnectionManager?.CleanUpBulkInsert(DestinationTableDefinition?.Name);

        _ownBulkInsertConnectionManager?.Dispose(); // initialized when LeaveOpen == false
        _ownBulkInsertConnectionManager = null;
    }
}
