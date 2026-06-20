using System.Linq;
using System.Text.RegularExpressions;

using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.ControlFlow;
using EtlKit.ConnectionManager;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Creates a table. If the tables exists, this task won't change the table.
    /// </summary>
    /// <example>
    /// <code>
    /// CreateTableTask.Create("demo.table1", new List&lt;TableColumn&gt;() {
    /// new TableColumn(name:"key", dataType:"int", allowNulls:false, isPrimaryKey:true, isIdentity:true),
    ///     new TableColumn(name:"value", dataType:"nvarchar(100)", allowNulls:true)
    /// });
    /// </code>
    /// </example>
    [PublicAPI]
    public class CreateTableTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => $"Create table {TableName}";

        public void Execute()
        {
            CheckTableDefinition();

            var tableExists = new IfTableOrViewExistsTask(TableName)
            {
                ConnectionManager = ConnectionManager,
                DisableLogging = true
            }.Exists();

            switch (tableExists)
            {
                case true when ThrowErrorIfTableExists:
                    throw new EtlKitException($"Table {TableName} already exists!");
                case false:
                    new SqlTask(this, Sql).ExecuteNonQuery();
                    break;
            }
        }

        /* Public properties */
        public void Create() => Execute();

        public TableDefinition TableDefinition { get; set; }
        public ObjectNameDescriptor TN => new(TableName, QB, QE);
        public List<TableColumn> Columns
        {
            get => TableDefinition.Columns;
            set => TableDefinition.Columns = value;
        }
        public string TableName
        {
            get => TableDefinition.Name;
            set => TableDefinition.Name = value;
        }

        public bool ThrowErrorIfTableExists { get; set; }

        public string Sql =>
            $@"
        CREATE TABLE {TN.QuotedFullName} (
{ColumnsDefinitionSql}
{PrimaryKeySql}
) {AddEngine()}
{OrderBy()}
{CreateFinallyComments(TableDefinition)}";

        private string AddEngine() =>
            ConnectionType == ConnectionManagerType.ClickHouse
                ? $"ENGINE = {TableDefinition?.Engine ?? "MergeTree()"}"
                : "";

        private string OrderBy() =>
            ConnectionType == ConnectionManagerType.ClickHouse && !Columns.Exists(c => c.IsPrimaryKey)
                ? $"ORDER BY {TableDefinition?.OrderBy ?? Columns[0].Name}"
                : "";

        public CreateTableTask() { }

        public CreateTableTask(string tableName, List<TableColumn> columns)
            : this()
        {
            TableDefinition = new TableDefinition(tableName, columns);
        }

        public CreateTableTask(TableDefinition tableDefinition)
            : this()
        {
            TableDefinition = tableDefinition;
        }

        public static void Create(string tableName, List<TableColumn> columns) =>
            new CreateTableTask(tableName, columns).Execute();

        public static void Create(TableDefinition tableDefinition) =>
            new CreateTableTask(tableDefinition).Execute();

        public static void Create(
            IConnectionManager connectionManager,
            string tableName,
            List<TableColumn> columns
        ) =>
            new CreateTableTask(tableName, columns)
            {
                ConnectionManager = connectionManager
            }.Execute();

        public static void Create(
            IConnectionManager connectionManager,
            TableDefinition tableDefinition
        ) =>
            new CreateTableTask(tableDefinition)
            {
                ConnectionManager = connectionManager
            }.Execute();

        private string ColumnsDefinitionSql =>
            Columns == null
                ? string.Empty
                : string.Join("  , " + Environment.NewLine, Columns.Select(CreateTableDefinition));

        private void CheckTableDefinition()
        {
            if (string.IsNullOrEmpty(TableName))
                throw new EtlKitException(
                    "No table name was provided - can not create or alter the table."
                );
            if (Columns == null || Columns.Count == 0)
                throw new EtlKitException(
                    "You did not provide any columns for the table - please define at least one table column."
                );
            if (Columns.Exists(col => string.IsNullOrEmpty(col.Name)))
                throw new EtlKitException(
                    "One of the provided columns is either null or empty - can't create table."
                );
            if (Columns.Exists(col => string.IsNullOrEmpty(col.DataType)))
                throw new EtlKitException(
                    "One of the provided columns has a datatype that is either null or empty - can't create table."
                );
            if (
                ConnectionType == ConnectionManagerType.ClickHouse
                && Columns.Exists(col => col.IsIdentity)
            )
                throw new ETLBoxNotSupportedException(
                    "ClickHouse does not support identity columns."
                );
        }

        private string PrimaryKeySql => CreatePrimaryKeyConstraint();

        /// <summary>
        ///  Тoлько для ClickHouse
        /// </summary>
        public string Engine
        {
            get => TableDefinition?.Engine;
            set
            {
                TableDefinition = TableDefinition ?? new TableDefinition();
                TableDefinition.Engine = value;
            }
        }

        private string CreateTableDefinition(ITableColumn col)
        {
            var (dataType, identitySql) = CreateTypeAndIdentitySql(col);
            var collationSql = !string.IsNullOrWhiteSpace(col.Collation)
                ? $"COLLATE {col.Collation}"
                : string.Empty;
            var nullSql = CreateNotNullSql(col);
            var defaultSql = CreateDefaultSql(col);
            var computedColumnSql = CreateComputedColumnSql(col);
            var comment = CreateCommentSql(col);
            return $"{QB}{col.Name}{QE} {dataType} {collationSql} {defaultSql} {identitySql} {nullSql} {computedColumnSql} {comment}";
        }

        private (string type, string identitySql) CreateTypeAndIdentitySql(ITableColumn col)
        {
            var type = ConnectionType switch
            {
                ConnectionManagerType.SqlServer when col.HasComputedColumn => string.Empty,
                _ => DataTypeConverter.TryGetDBSpecificType(col, ConnectionType)
            };
            if (!col.IsIdentity)
            {
                return (type, string.Empty);
            }
            var identitySql = ConnectionType switch
            {
                ConnectionManagerType.ClickHouse or ConnectionManagerType.SQLite => string.Empty,
                ConnectionManagerType.MySql => "AUTO_INCREMENT",
                ConnectionManagerType.Postgres => 
                    $"GENERATED BY DEFAULT AS IDENTITY (MINVALUE {col.IdentitySeed ?? 1} INCREMENT BY {col.IdentityIncrement ?? 1})",
                _ => $"IDENTITY({col.IdentitySeed ?? 1},{col.IdentityIncrement ?? 1})"
            };
            return (type, identitySql);
        }

        private string CreateNotNullSql(ITableColumn col)
        {
            switch (ConnectionType)
            {
                case ConnectionManagerType.Postgres when col.IsIdentity:
                case ConnectionManagerType.Access:
                case ConnectionManagerType.MySql:
                    return string.Empty;
                case ConnectionManagerType.SQLite when col.AllowNulls:
                    return string.Empty;
                case ConnectionManagerType.ClickHouse when col.AllowNulls:
                    return string.Empty;
                default:
                    if (string.IsNullOrWhiteSpace(col.ComputedColumn))
                        return col.AllowNulls ? "NULL" : "NOT NULL";
                    return string.Empty;
            }
        }

        private string CreatePrimaryKeyConstraint()
        {
            var result = string.Empty;
            if (!(Columns?.Exists(col => col.IsPrimaryKey) ?? false))
            {
                return result;
            }

            var pkCols = Columns.Where(col => col.IsPrimaryKey).ToArray();
            var pkConstName =
                TableDefinition.PrimaryKeyConstraintName
                ?? $"pk_{TN.UnquotedFullName}_{string.Join("_", pkCols.Select(col => col.Name))}";

            var constraint = ConnectionType switch
            {
                ConnectionManagerType.ClickHouse or ConnectionManagerType.SQLite => "",
                _ => $"CONSTRAINT {QB}{pkConstName}{QE}"
            };
            var pkConst =
                $", {constraint} PRIMARY KEY ({string.Join(",", pkCols.Select(col => $"{QB}{col.Name}{QE}"))})";
            return pkConst;
        }

        private static string CreateDefaultSql(ITableColumn col)
        {
            var defaultSql = string.Empty;
            if (!col.IsPrimaryKey)
                defaultSql =
                    col.DefaultValue != null
                        ? $" DEFAULT {SetQuotesIfString(col.DefaultValue)}"
                        : string.Empty;
            return defaultSql;
        }

        private string CreateComputedColumnSql(ITableColumn col)
        {
            return col.HasComputedColumn switch
            {
                true when !DbConnectionManager.SupportComputedColumns
                    => throw new ETLBoxNotSupportedException("Computed columns are not supported."),
                true => $"AS ({col.ComputedColumn})",
                _ => string.Empty
            };
        }

        private string CreateCommentSql(ITableColumn col)
        {
            if (!string.IsNullOrWhiteSpace(col.Comment)
            && (
                   ConnectionType == ConnectionManagerType.MySql
                || ConnectionType == ConnectionManagerType.ClickHouse
            ))
            { 
                return $"COMMENT '{col.Comment}'";
            }
            return string.Empty;
        }

        private string CreateFinallyComments(TableDefinition table) =>
            string.Join(
                ";\n",
                table.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Comment)).Select(GetComments)
            );

        private string GetComments(TableColumn c)
        {
            return ConnectionManager.ConnectionManagerType switch
            {
                ConnectionManagerType.SqlServer
                    => $@"
    --ColumnDescription
    exec sp_addextendedproperty
        @name = 'ColumnDescription', 
        @value = '{c.Comment}', 
        @level0type = N'SCHEMA', 
        @level0name = N'{GetSchema()}', 
        @level1type = N'TABLE', 
        @level1name = N'{TN.UnquotedObjectName}', 
        @level2type = N'COLUMN', 
        @level2name = N'{c.Name}';
    ",
                ConnectionManagerType.Postgres
                or ConnectionManagerType.SQLite
                    => $"comment on column {TN.QuotedFullName}.{QB}{c.Name}{QE} is '{c.Comment}'",
                _ => null
            };
        }

        private string GetSchema()
        {
            if (string.IsNullOrEmpty(TN.UnquotedSchemaName))
            {
                return "dbo";
            }
            return TN.UnquotedSchemaName;
        }

        private static string SetQuotesIfString(string value)
        {
            return !Regex.IsMatch(value, @"^\d+(\.\d+|)$") //@" ^ (\d|\.)+$"))
                ? $"'{value}'"
                : value;
        }
    }
}
