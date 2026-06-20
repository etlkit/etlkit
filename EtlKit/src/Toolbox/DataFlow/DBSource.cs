using System.Linq;
using System.Threading;

using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;
using EtlKit.Helper;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// A database source defines either a table or sql query that returns data from a database.
    /// While reading the result set or the table, data is asnychronously posted
    /// into the targets.
    /// </summary>
    /// <typeparam name="TOutput">Type of data output.</typeparam>
    /// <example>
    /// <code>
    /// DbSource&lt;MyRow&gt; source = new DbSource&lt;MyRow&gt;("dbo.table");
    /// source.LinkTo(dest); //Transformation or Destination
    /// source.Execute(); //Start the data flow
    /// </code>
    /// </example>
    [PublicAPI]
    public class DbSource<TOutput> : DataFlowSource<TOutput>, IDataFlowSource<TOutput>
    {
        /* ITask Interface */
        public override string TaskName => $"Read data from {SourceDescription}";

        /* Public Properties */
        public TableDefinition SourceTableDefinition { get; set; }
        public List<string> ColumnNames { get; set; }
        public string TableName { get; set; }
        public string Sql { get; set; }

        public string SqlForRead
        {
            get
            {
                if (HasSql)
                    return Sql;
                if (!HasSourceTableDefinition)
                    LoadTableDefinition();
                var tn = new ObjectNameDescriptor(SourceTableDefinition.Name, QB, QE);
                return $@"SELECT {SourceTableDefinition.Columns.AsString("", QB, QE)} FROM {tn.QuotedFullName}";
            }
        }

        public IEnumerable<string> ColumnNamesEvaluated
        {
            get
            {
                if (ColumnNames?.Count > 0)
                    return ColumnNames;

                return HasSourceTableDefinition
                    ? SourceTableDefinition?.Columns?.Select(col => col.Name)
                    : ParseColumnNamesFromQuery();
            }
        }

        private bool HasSourceTableDefinition => SourceTableDefinition != null;
        private bool HasTableName => !string.IsNullOrWhiteSpace(TableName);
        private bool HasSql => !string.IsNullOrWhiteSpace(Sql);
        private DBTypeInfo TypeInfo { get; set; }

        private string SourceDescription
        {
            get =>
                (HasSourceTableDefinition, HasTableName) switch
                {
                    (true, _) => $"table {SourceTableDefinition.Name}",
                    (_, true) => $"table {TableName}",
                    (_, _) => "custom sql",
                };
        }

        public DbSource()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public DbSource([CanBeNull] ILogger<DbSource<TOutput>> logger)
            : base(logger)
        {
            TypeInfo = new DBTypeInfo(typeof(TOutput));
        }

        public DbSource(string tableName)
            : this()
        {
            TableName = tableName;
        }

        public DbSource(IConnectionManager connectionManager)
            : this()
        {
            ConnectionManager = connectionManager;
        }

        public DbSource(IConnectionManager connectionManager, string tableName)
            : this(tableName)
        {
            ConnectionManager = connectionManager;
        }

        private List<string> ParseColumnNamesFromQuery()
        {
            var result = SqlParser.ParseColumnNames(
                QB != string.Empty ? SqlForRead.Replace(QB, "").Replace(QE, "") : SqlForRead
            );
            if (TypeInfo.IsArray && result?.Count == 0)
                throw new EtlKitException(
                    "Could not parse column names from Sql Query! Please pass a valid TableDefinition to the "
                        + " property SourceTableDefinition with at least a name for each column that you want to use in the source."
                );
            return result;
        }

        public override void Execute(CancellationToken cancellationToken)
        {
            LogStart();
            try
            {
                ReadAll();
                Buffer.Complete();
            }
            catch (Exception e)
            {
                ((IDataflowBlock)Buffer).Fault(e);
                throw;
            }
            Buffer.Complete();
            LogFinish();
        }

        private void ReadAll()
        {
            SqlTask sqlT = CreateSqlTask(SqlForRead);
            DefineActions(sqlT, ColumnNamesEvaluated.ToList());
            sqlT.ExecuteReader();
            CleanupSqlTask(sqlT);
        }

        private void LoadTableDefinition()
        {
            if (HasTableName)
                SourceTableDefinition = TableDefinition.GetDefinitionFromTableName(
                    DbConnectionManager,
                    TableName
                );
            else if (!HasSourceTableDefinition && !HasTableName)
                throw new EtlKitException(
                    "No Table definition or table name found! You must provide a table name or a table definition."
                );
        }

        private SqlTask CreateSqlTask(string sql) =>
            new(this, sql) { DisableLogging = true, Actions = new List<Action<object>>() };

        private TOutput _row;

        internal void DefineActions(SqlTask sqlT, List<string> columnNames)
        {
            _row = default;
            if (TypeInfo.IsArray)
            {
                // Create array buffer of given size
                sqlT.BeforeRowReadAction = () =>
                    _row = (TOutput)Activator.CreateInstance(typeof(TOutput), columnNames.Count);
                // Set up copy action for each column
                for (var i = 0; i < columnNames.Count; i++)
                {
                    var currentIndexAvoidingClosure = i;
                    sqlT.Actions!.Add(col =>
                    {
                        CopyColumnToArray(col, currentIndexAvoidingClosure);
                    });
                }
            }
            else
            {
                // Create object row buffer of given type
                sqlT.BeforeRowReadAction = () =>
                    _row = (TOutput)Activator.CreateInstance(typeof(TOutput));
                // Fill column names from object properties if needed
                if (columnNames?.Count is 0 or null)
                    columnNames = TypeInfo.PropertyNames;
                // Set up copy actions for all columns
                sqlT.Actions!.AddRange(columnNames.Select(GenerateColumnCopyAction));
            }
            sqlT.AfterRowReadAction = () =>
            {
                if (_row is not null)
                {
                    LogProgress();
                    Buffer.SendAsync(_row).Wait();
                }
            };
        }

        private Action<object> GenerateColumnCopyAction(string colName) =>
            (TypeInfo.IsDynamic, TypeInfo.HasPropertyOrColumnMapping(colName)) switch
            {
                (_, true) => colValue =>
                {
                    CopyColumnToObjectWithReflection(colName, colValue);
                },
                (true, false) => colValue =>
                {
                    CopyColumnToDynamicObject(colName, colValue);
                },
                (_, _) => _ => { },
            };

        private void CopyColumnToArray(object columnValue, int columnIndex)
        {
            try
            {
                if (_row is not null)
                {
                    var ar = _row as Array;
                    var con = Convert.ChangeType(columnValue, typeof(TOutput).GetElementType()!);
                    ar!.SetValue(con, columnIndex);
                }
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                _row = default;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(_row));
            }
        }

        private void CopyColumnToObjectWithReflection(string colName, object colValue)
        {
            try
            {
                if (_row is null)
                    return;

                var propInfo = TypeInfo.GetInfoByPropertyNameOrColumnMapping(colName);

                var con = (colValue, TypeInfo.UnderlyingPropType[propInfo].IsEnum) switch
                {
                    (null, _) => null,
                    (_, true) => colValue,
                    (_, _) => Convert.ChangeType(colValue, TypeInfo.UnderlyingPropType[propInfo]),
                };

                propInfo.TrySetValue(_row, con, TypeInfo.UnderlyingPropType[propInfo]);
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                _row = default;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(_row));
            }
        }

        private void CopyColumnToDynamicObject(string colName, object colValue)
        {
            try
            {
                if (_row is null)
                {
                    return;
                }

                IDictionary<string, object> r = _row as ExpandoObject;
                r!.Add(colName, colValue);
            }
            catch (Exception e)
            {
                if (!ErrorHandler.HasErrorBuffer)
                    throw;
                _row = default;
                ErrorHandler.Send(e, ErrorHandler.ConvertErrorData(_row));
            }
        }

        private static void CleanupSqlTask(SqlTask sqlT)
        {
            sqlT.Actions = null;
        }
    }

    /// <summary>
    /// A database source defines either a table or sql query that returns data from a database. While reading the result set or the table, data is asnychronously posted
    /// into the targets. The non generic version of the DbSource uses a dynamic object that contains the data.
    /// </summary>
    /// <see cref="DbSource{TOutput}"/>
    /// <example>
    /// <code>
    /// DbSource source = new DbSource("dbo.table");
    /// source.LinkTo(dest); //Transformation or Destination
    /// source.Execute(); //Start the data flow
    /// </code>
    /// </example>
    [PublicAPI]
    public class DbSource : DbSource<ExpandoObject>
    {
        public DbSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public DbSource(ILogger<DbSource> logger)
            : base(logger) { }

        public DbSource(string tableName)
            : base(tableName) { }

        public DbSource(IConnectionManager connectionManager)
            : base(connectionManager) { }

        public DbSource(IConnectionManager connectionManager, string tableName)
            : base(connectionManager, tableName) { }
    }
}
