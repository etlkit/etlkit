using EtlKit.ConnectionManager;

namespace EtlKit
{
    /// <summary>
    /// Describes one column of a <see cref="TableDefinition"/>: its name, SQL data type, and
    /// constraints. Also implements <see cref="IColumnMapping"/> so a list of columns can be used
    /// directly as an ADO.NET column mapping source.
    /// </summary>
    public class TableColumn : ITableColumn, IColumnMapping
    {
        private string _dataSetColumn;
        private string _sourceColumn;

        /// <summary>
        /// The column's name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The column's SQL data type (e.g. <c>"INT"</c>, <c>"VARCHAR(50)"</c>).
        /// </summary>
        public string DataType { get; set; }
        internal string InternalDataType { get; set; }

        /// <summary>
        /// Whether the column allows <c>NULL</c> values.
        /// </summary>
        public bool AllowNulls { get; set; }

        /// <summary>
        /// Whether the column is an auto-incrementing identity column.
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// Whether the column is (part of) the table's primary key.
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// The column's default value expression, or <see langword="null"/> if it has none.
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// The column's collation, or <see langword="null"/> if not set/not applicable.
        /// </summary>
        public string Collation { get; set; }

        /// <summary>
        /// The computed-column expression, or <see langword="null"/> if the column is not computed.
        /// </summary>
        public string ComputedColumn { get; set; }

        /// <summary>
        /// Whether <see cref="ComputedColumn"/> is set.
        /// </summary>
        public bool HasComputedColumn => !string.IsNullOrWhiteSpace(ComputedColumn);

        /// <summary>
        /// The .NET <see cref="Type"/> equivalent of <see cref="DataType"/>.
        /// </summary>
        public Type NETDataType => Type.GetType(DataTypeConverter.GetNETObjectTypeString(DataType));

        /// <summary>
        /// The <see cref="System.DateTimeKind"/> implied by <see cref="DataType"/>, for date/time
        /// columns; <see langword="null"/> for non-date/time columns.
        /// </summary>
        public DateTimeKind? NETDateTimeKind => DataTypeConverter.GetNETDateTimeKind(DataType);

        /// <summary>
        /// Column comment. MySQL only.
        /// </summary>
        public string Comment { get; set; } //MySql only

        /// <summary>
        /// Identity seed value. SQL Server only.
        /// </summary>
        public int? IdentitySeed { get; set; } //Sql Server only

        /// <summary>
        /// Identity increment value. SQL Server only.
        /// </summary>
        public int? IdentityIncrement { get; set; } //Sql Server only

        /// <inheritdoc />
        /// <remarks>
        /// Defaults to <see cref="Name"/> until explicitly set.
        /// </remarks>
        public string DataSetColumn
        {
            get { return string.IsNullOrWhiteSpace(_dataSetColumn) ? Name : _dataSetColumn; }
            set { _dataSetColumn = value; }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Defaults to <see cref="Name"/> until explicitly set.
        /// </remarks>
        public string SourceColumn
        {
            get { return string.IsNullOrWhiteSpace(_sourceColumn) ? Name : _sourceColumn; }
            set { _sourceColumn = value; }
        }

        /// <summary>
        /// Creates a column with no name or data type set yet.
        /// </summary>
        public TableColumn() { }

        /// <summary>
        /// Creates a column with the given name and data type.
        /// </summary>
        /// <param name="name">The column's name.</param>
        /// <param name="dataType">The column's SQL data type.</param>
        public TableColumn(string name, string dataType)
            : this()
        {
            Name = name;
            DataType = dataType;
        }

        /// <summary>
        /// Creates a column with the given name, data type, and nullability.
        /// </summary>
        /// <param name="name">The column's name.</param>
        /// <param name="dataType">The column's SQL data type.</param>
        /// <param name="allowNulls">Whether the column allows <c>NULL</c> values.</param>
        public TableColumn(string name, string dataType, bool allowNulls)
            : this(name, dataType)
        {
            AllowNulls = allowNulls;
        }

        /// <summary>
        /// Creates a column with the given name, data type, nullability, and primary key flag.
        /// </summary>
        /// <param name="name">The column's name.</param>
        /// <param name="dataType">The column's SQL data type.</param>
        /// <param name="allowNulls">Whether the column allows <c>NULL</c> values.</param>
        /// <param name="isPrimaryKey">Whether the column is (part of) the primary key.</param>
        public TableColumn(string name, string dataType, bool allowNulls, bool isPrimaryKey)
            : this(name, dataType, allowNulls)
        {
            IsPrimaryKey = isPrimaryKey;
        }

        /// <summary>
        /// Creates a column with the given name, data type, nullability, primary key flag, and
        /// identity flag.
        /// </summary>
        /// <param name="name">The column's name.</param>
        /// <param name="dataType">The column's SQL data type.</param>
        /// <param name="allowNulls">Whether the column allows <c>NULL</c> values.</param>
        /// <param name="isPrimaryKey">Whether the column is (part of) the primary key.</param>
        /// <param name="isIdentity">Whether the column is an auto-incrementing identity column.</param>
        public TableColumn(
            string name,
            string dataType,
            bool allowNulls,
            bool isPrimaryKey,
            bool isIdentity
        )
            : this(name, dataType, allowNulls, isPrimaryKey)
        {
            IsIdentity = isIdentity;
        }
    }
}
