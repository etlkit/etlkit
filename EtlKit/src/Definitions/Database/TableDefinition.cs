using System.Linq;

using EtlKit.Helper;
using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;

namespace EtlKit
{
    [PublicAPI]
    public class TableDefinition
    {
        public string Name { get; set; }
        public List<TableColumn> Columns { get; set; }
        public string PrimaryKeyConstraintName { get; set; }

        // Only for ClickHouse
        public string OrderBy { get; set; }

        public int? IDColumnIndex
        {
            get
            {
                TableColumn idCol = Columns.Find(col => col.IsIdentity);
                if (idCol != null)
                    return Columns.IndexOf(idCol);
                return null;
            }
        }

        public TableDefinition()
        {
            Columns = new List<TableColumn>();
        }

        public TableDefinition(string name)
            : this()
        {
            Name = name;
        }

        public TableDefinition(string name, List<TableColumn> columns)
            : this(name)
        {
            Columns = columns;
        }

        /// <summary>
        /// Only for ClickHouse
        /// </summary>
        public string Engine { get; set; }

        private static readonly string[] trueArray = new[] { "yes", "true", "on", "1", "да" };

        public void CreateTable(IConnectionManager connectionManager) =>
            CreateTableTask.Create(connectionManager, this);

        public static TableDefinition GetDefinitionFromTableName(
            IConnectionManager connectionManager,
            string tableName
        )
        {
            // Clone the connection manager for the case, when original manager already has connection with open cursor
            using var metadataConnection = connectionManager.Clone();
            IfTableOrViewExistsTask.ThrowExceptionIfNotExists(metadataConnection, tableName);
            ConnectionManagerType connectionType = metadataConnection.ConnectionManagerType;
            var tn = new ObjectNameDescriptor(
                tableName,
                metadataConnection.QB,
                metadataConnection.QE
            );

            return connectionType switch
            {
                ConnectionManagerType.SqlServer => ReadTableDefinitionFromSqlServer(
                    metadataConnection,
                    tn
                ),
                ConnectionManagerType.SQLite => ReadTableDefinitionFromSQLite(
                    metadataConnection,
                    tn
                ),
                ConnectionManagerType.MySql => ReadTableDefinitionFromMySqlServer(
                    metadataConnection,
                    tn
                ),
                ConnectionManagerType.Postgres => ReadTableDefinitionFromPostgres(
                    metadataConnection,
                    tn
                ),
                ConnectionManagerType.Access => ReadTableDefinitionFromAccess(
                    metadataConnection,
                    tn
                ),
                ConnectionManagerType.ClickHouse => ReadTableDefinitionFromClickHouse(
                    metadataConnection,
                    tn
                ),
                _ => throw new EtlKitException(
                    "Unknown connection type - please pass a valid TableDefinition!"
                ),
            };
        }

        private static TableDefinition ReadTableDefinitionFromClickHouse(
            IConnectionManager connection,
            ObjectNameDescriptor tn
        )
        {
            var result = new TableDefinition(tn.ObjectName);
            TableColumn curCol = null;

            var readMetaSql = new SqlTask(
                $"Read column meta data for table {tn.ObjectName}",
                $@"
                SELECT 
                     c.name
                    ,c.type as type
                    ,c.is_in_primary_key as primary_key
                    ,c.default_expression as default_value
                    ,ic.is_nullable
                    ,c.comment
                FROM system.columns c
                LEFT JOIN information_schema.columns as ic
                    ON ic.table_catalog= c.database
                    AND ic.table_name = c.table
                    AND ic.column_name = c.name
                WHERE c.database = currentDatabase()
                  AND c.table = '{tn.ObjectName}'",
                () =>
                {
                    curCol = new TableColumn();
                },
                () =>
                {
                    result.Columns.Add(curCol);
                },
                name => curCol.Name = name.ToString(),
                type => curCol.DataType = type.ToString(),
                primaryKey => curCol.IsPrimaryKey = ParseBoolean(primaryKey),
                defaultValue => curCol.DefaultValue = defaultValue?.ToString(),
                is_nullable => curCol.AllowNulls = ParseBoolean(is_nullable),
                comment => curCol.Comment = comment?.ToString()
            )
            {
                DisableLogging = true,
                ConnectionManager = connection,
            };
            readMetaSql.ExecuteReader();
            return result;
        }

        private static TableDefinition ReadTableDefinitionFromSqlServer(
            IConnectionManager connection,
            ObjectNameDescriptor tn
        )
        {
            var result = new TableDefinition(tn.ObjectName);
            TableColumn curCol = null;

            var readMetaSql = new SqlTask(
                $"Read column meta data for table {tn.ObjectName}",
                // language=sql
                $"""

                SELECT  cols.name
                     , CASE WHEN tpes.name IN ('varchar','char','binary','varbinary') THEN CONCAT(UPPER(tpes.name), '(', cols.max_length, ')')
                            WHEN tpes.name IN ('nvarchar','nchar') THEN CONCAT(UPPER(tpes.name), '(', cols.max_length/2, ')')
                            WHEN tpes.name IN ('decimal','numeric') THEN CONCAT(UPPER(tpes.name), '(', cols.precision,',',cols.scale, ')')
                            ELSE UPPER(tpes.name)            
                       END AS type_name
                     , cols.is_nullable
                     , cols.is_identity
                     , ident.seed_value
                     , ident.increment_value
                     , CONVERT (BIT, CASE WHEN pkidxcols.index_column_id IS NOT NULL THEN 1 ELSE 0 END ) AS primary_key
                     , defconstr.definition AS default_value
                     , cols.collation_name
                     , compCol.definition AS computed_column_definition
                     , comment.value AS comment
                FROM sys.columns cols
                INNER JOIN (
                    SELECT name, type, object_id, schema_id FROM sys.tables 
                    UNION 
                    SELECT  name, type, object_id, schema_id FROM sys.views
                    ) tbl
                    ON cols.object_id = tbl.object_id
                INNER JOIN sys.schemas sc
                    ON tbl.schema_id = sc.schema_id
                INNER JOIN sys.systypes tpes
                    ON tpes.xusertype = cols.user_type_id
                LEFT JOIN sys.identity_columns ident
                    ON ident.object_id = cols.object_id
                LEFT JOIN sys.indexes pkidx
                    ON pkidx.object_id = cols.object_id
                    AND pkidx.is_primary_key = 1
                LEFT JOIN sys.index_columns pkidxcols
                    on pkidxcols.object_id = cols.object_id
                    AND pkidxcols.column_id = cols.column_id
                    AND pkidxcols.index_id = pkidx.index_id
                LEFT JOIN sys.default_constraints defconstr
                    ON defconstr.parent_object_id = cols.object_id
                    AND defconstr.parent_column_id = cols.column_id
                LEFT JOIN sys.computed_columns compCol
                    ON compCol.object_id = cols.object_id
                {GetComment()}
                WHERE ( CONCAT (sc.name,'.',tbl.name) ='{tn.UnquotedFullName}' OR  tbl.name = '{tn.UnquotedFullName}' )
                    AND tbl.type IN ('U','V')
                    AND tpes.name <> 'sysname'
                ORDER BY cols.column_id
                
                """,
                beforeRowReadAction: () => curCol = new TableColumn(),
                afterRowReadAction: () => result.Columns.Add(curCol),
                name => curCol.Name = name.ToString(),
                typeName => curCol.DataType = typeName.ToString(),
                isNullable => curCol.AllowNulls = (bool)isNullable,
                isIdentity => curCol.IsIdentity = (bool)isIdentity,
                seedValue => curCol.IdentitySeed = Convert.ToInt32(seedValue),
                incrementValue => curCol.IdentityIncrement = Convert.ToInt32(incrementValue),
                primaryKey => curCol.IsPrimaryKey = (bool)primaryKey,
                defaultValue =>
                    curCol.DefaultValue = defaultValue
                        ?.ToString()
                        .Substring(2, defaultValue.ToString().Length - 4),
                collationName => curCol.Collation = collationName?.ToString(),
                computedColumnDefinition =>
                    curCol.ComputedColumn = computedColumnDefinition
                        ?.ToString()
                        .Substring(1, computedColumnDefinition.ToString().Length - 2),
                comment => curCol.Comment = comment?.ToString()
            )
            {
                DisableLogging = true,
                ConnectionManager = connection,
            };
            readMetaSql.ExecuteReader();
            return result;
        }

        private static string GetComment() =>
            $"outer apply fn_listextendedproperty('ColumnDescription', 'schema', sc.name, 'table', tbl.name, 'column', cols.name) as comment";

        private static TableDefinition ReadTableDefinitionFromSQLite(
            IConnectionManager connection,
            ObjectNameDescriptor tn
        )
        {
            var result = new TableDefinition(tn.ObjectName);
            TableColumn curCol = null;
            var readMetaSql = new SqlTask(
                $"Read column meta data for table {tn.ObjectName}",
                $@"
                SELECT
                  c.name AS column_name,
                  c.type AS column_type,
                  CASE WHEN c.pk > 0 THEN 1 ELSE 0 END AS is_primary_key,
                  c.dflt_value as default_value,
                  CASE WHEN c.""notnull"" = 0 THEN 1 ELSE 0 END AS is_nullable
                FROM
                      sqlite_master AS m
                    JOIN
                      pragma_table_info(m.name) AS c ON true
                    LEFT JOIN
                      pragma_index_info AS pk ON c.cid = pk.cid
                WHERE m.type in ('table', 'view')
                  AND m.name = '{tn.UnquotedFullName}'
                ORDER BY
                  c.cid;",
                () =>
                {
                    curCol = new TableColumn();
                },
                () =>
                {
                    result.Columns.Add(curCol);
                },
                name => curCol.Name = name.ToString(),
                type => curCol.DataType = type.ToString(),
                pk =>
                    curCol.IsPrimaryKey =
                        (long.TryParse(pk?.ToString(), out long res) ? res : 0) >= 1,
                defaultValue => curCol.DefaultValue = defaultValue?.ToString(),
                is_nullable => curCol.AllowNulls = ParseBoolean(is_nullable)
            )
            {
                DisableLogging = true,
                ConnectionManager = connection,
            };
            readMetaSql.ExecuteReader();
            return result;
        }

        private static TableDefinition ReadTableDefinitionFromMySqlServer(
            IConnectionManager connection,
            ObjectNameDescriptor tn
        )
        {
            var result = new TableDefinition(tn.ObjectName);
            TableColumn curCol = null;

            var readMetaSql = new SqlTask(
                $"Read column meta data for table {tn.ObjectName}",
                $@" 
SELECT cols.column_name
  , CASE WHEN cols.data_type IN ('varchar','char') THEN CONCAT (cols.data_type,'(',cols.character_maximum_length, ')')
	     WHEN cols.data_type IN ('decimal') THEN CONCAT (cols.data_type,'(',cols.numeric_precision,',', cols.numeric_scale, ')')
		 ELSE cols.data_type
         END AS 'data_type'
  , CASE WHEN cols.is_nullable = 'NO' THEN 0 ELSE 1 END AS 'is_nullable'
  , CASE WHEN cols.extra IS NOT NULL AND cols.extra = 'auto_increment' THEN 1 ELSE 0 END AS 'auto_increment'
  , CASE WHEN isnull(k.constraint_name) THEN 0 ELSE 1 END AS 'primary_key'
  , cols.column_default
  , cols.collation_name
  , cols.generation_expression
  , cols.column_comment
FROM INFORMATION_SCHEMA.COLUMNS cols
INNER JOIN  INFORMATION_SCHEMA.TABLES tbl
    ON cols.table_name = tbl.table_name
    AND cols.table_schema = tbl.table_schema
    AND cols.table_catalog = tbl.table_catalog
LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
    ON cols.table_name = k.table_name
    AND cols.table_schema = k.table_schema
    AND cols.table_catalog = k.table_catalog
    AND cols.column_name = k.column_name
    AND k.constraint_name = 'PRIMARY'
WHERE ( cols.table_name = '{tn.UnquotedFullName}'  OR  CONCAT(cols.table_catalog,'.',cols.table_name) = '{tn.UnquotedFullName}')
    AND cols.table_schema = DATABASE()
ORDER BY cols.ordinal_position
",
                () =>
                {
                    curCol = new TableColumn();
                },
                () =>
                {
                    result.Columns.Add(curCol);
                },
                columnName => curCol.Name = columnName.ToString(),
                dataType => curCol.DataType = dataType.ToString(),
                isNullable => curCol.AllowNulls = (int)isNullable == 1,
                autoIncrement => curCol.IsIdentity = (int)autoIncrement == 1,
                primaryKey => curCol.IsPrimaryKey = (int)primaryKey == 1,
                columnDefault => curCol.DefaultValue = columnDefault?.ToString(),
                collationName => curCol.Collation = collationName?.ToString(),
                generationExpression => curCol.ComputedColumn = generationExpression?.ToString(),
                comment => curCol.Comment = comment?.ToString()
            )
            {
                DisableLogging = true,
                ConnectionManager = connection,
            };
            readMetaSql.ExecuteReader();
            return result;
        }

        private static TableDefinition ReadTableDefinitionFromPostgres(
            IConnectionManager connection,
            ObjectNameDescriptor tn
        )
        {
            var result = new TableDefinition(tn.ObjectName);
            TableColumn curCol = null;

            var readMetaSql = new SqlTask(
                $"Read column meta data for table {tn.ObjectName}",
                $@" 
SELECT cols.column_name
,CASE 
   WHEN LEFT(cols.data_type,4) = 'time' THEN REPLACE(REPLACE(REPLACE(cols.data_type,'without time zone',''), 'with time zone', 'tz'),' ','')
   ELSE cols.data_type
END AS ""internaldatatype""
,CASE
    WHEN cols.domain_name IS NOT NULL THEN domain_name
    WHEN cols.data_type='character varying' THEN
        CASE WHEN character_maximum_length IS NULL
        THEN 'varchar'
        ELSE 'varchar('||character_maximum_length||')'
        END
    WHEN cols.data_type='character' THEN 'char('||character_maximum_length||')'
    WHEN cols.data_type='numeric' THEN
        CASE WHEN numeric_precision IS NULL
        THEN 'numeric'
        ELSE 'numeric('||numeric_precision||','||numeric_scale||')'
        END
    WHEN LEFT(cols.data_type,4) = 'time' THEN REPLACE(REPLACE(REPLACE(cols.data_type,'without time zone',''), 'with time zone', 'tz'),' ','')
    ELSE cols.data_type
END AS ""datatype""
, CASE WHEN cols.is_nullable = 'NO' THEN 0 ELSE 1 END AS ""is_nullable""
, CASE WHEN cols.column_default IS NOT NULL AND substring(cols.column_default,0,8) = 'nextval' THEN 1 ELSE 0 END AS ""serial""
, CASE WHEN tccu.column_name IS NULL THEN 0 ELSE 1 END AS ""primary_key""
, cols.column_default
, cols.collation_name
, cols.generation_expression
, pgd.description
, cols.is_identity
, cols.identity_start
, cols.identity_increment
FROM INFORMATION_SCHEMA.COLUMNS cols
INNER JOIN  INFORMATION_SCHEMA.TABLES tbl
    ON cols.table_name = tbl.table_name
    AND cols.table_schema = tbl.table_schema
    AND cols.table_catalog = tbl.table_catalog
LEFT JOIN INFORMATION_SCHEMA.table_constraints tc
    ON cols.table_name = tc.table_name
    AND cols.table_schema = tc.table_schema
    AND cols.table_catalog = tc.table_catalog
    AND tc.constraint_type = 'PRIMARY KEY'
LEFT JOIN information_schema.constraint_column_usage tccu
    ON cols.table_name = tccu.table_name
    AND cols.table_schema = tccu.table_schema
    AND cols.table_catalog = tccu.table_catalog
    AND tccu.constraint_name = tc.constraint_name
    AND tccu.constraint_schema = tc.constraint_schema
    AND tccu.constraint_catalog = tc.constraint_catalog
    AND cols.column_name = tccu.column_name
left join pg_catalog.pg_statio_all_tables as st
    on st.schemaname = cols.table_schema
    and st.relname = cols.table_name
left join pg_catalog.pg_description pgd
    on pgd.objoid = st.relid
    and  pgd.objsubid = cols.ordinal_position
WHERE(cols.table_name = '{tn.UnquotedFullName}'  OR  CONCAT(cols.table_schema, '.', cols.table_name) = '{tn.UnquotedFullName}')
    AND cols.table_catalog = CURRENT_DATABASE()
ORDER BY cols.ordinal_position
",
                () =>
                {
                    curCol = new TableColumn();
                },
                () =>
                {
                    result.Columns.Add(curCol);
                },
                columnName => curCol.Name = columnName.ToString(),
                internalTypeName => curCol.InternalDataType = internalTypeName.ToString(),
                dataType => curCol.DataType = dataType.ToString(),
                isNullable => curCol.AllowNulls = (int)isNullable == 1,
                serial => curCol.IsIdentity = (int)serial == 1,
                primaryKey => curCol.IsPrimaryKey = (int)primaryKey == 1,
                columnDefault =>
                    curCol.DefaultValue = columnDefault
                        ?.ToString()
                        .ReplaceIgnoreCase("::character varying", ""),
                collationName => curCol.Collation = collationName?.ToString(),
                generationExpression => curCol.ComputedColumn = generationExpression?.ToString(),
                comment => curCol.Comment = comment?.ToString(),
                is_identity => curCol.IsIdentity = ParseBoolean(is_identity),
                identity_start =>
                    curCol.IdentitySeed = int.TryParse(identity_start?.ToString(), out int start)
                        ? start
                        : null,
                identity_increment =>
                    curCol.IdentityIncrement = int.TryParse(
                        identity_increment?.ToString(),
                        out int inc
                    )
                        ? inc
                        : null
            )
            {
                DisableLogging = true,
                ConnectionManager = connection,
            };
            readMetaSql.ExecuteReader();
            return result;
        }

        private static bool ParseBoolean(object value)
        {
            if (value is null)
            {
                return false;
            }
            return trueArray.Contains(value.ToString().ToLower());
        }

        private static TableDefinition ReadTableDefinitionFromAccess(
            IConnectionManager connection,
            ObjectNameDescriptor tn
        )
        {
            var accessConnection = connection as AccessOdbcConnectionManager;
            return accessConnection?.ReadTableDefinition(tn);
        }
    }
}
