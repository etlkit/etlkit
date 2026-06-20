using System.Linq;

using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow.SqlServer
{
    /// <summary>
    /// Calculates a hash value of the database. It will use only the schemas given in the property SchemaName for the calculation.
    /// The hash calcualtion is based only on the user tables in the schema.
    /// </summary>
    /// <example>
    /// <code>
    /// CalculateDatabaseHashTask.Calculate(new List&lt;string&gt;() { "demo", "dbo" });
    /// </code>
    /// </example>
    [PublicAPI]
    public class CalculateDatabaseHashTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName =>
            $"Calculate hash value for schema(s) {SchemaNamesAsString}";

        public void Execute()
        {
            if (!DbConnectionManager.SupportSchemas)
                throw new ETLBoxNotSupportedException("This task is not supported!");

            var allColumns = new List<string>();
            new SqlTask(this, Sql)
            {
                Actions = new List<Action<object>> { col => allColumns.Add((string)col) }
            }.ExecuteReader();
            DatabaseHash = HashHelper.Encrypt_Char40(string.Join("|", allColumns));
        }

        /* Public properties */
        public List<string> SchemaNames { get; set; }

        public string DatabaseHash { get; private set; }

        private string SchemaNamesAsString =>
            string.Join(",", SchemaNames.Select(name => $"'{name}'"));
        public string Sql =>
            $@"
SELECT sch.name + '.' + tbls.name + N'|' + 
	   cols.name + N'|' + 
	   typ.name + N'|' + 
	   CAST(cols.max_length AS nvarchar(20))+ N'|' + 
	   CAST(cols.precision AS nvarchar(20)) + N'|' + 
	   CAST(cols.scale AS nvarchar(20)) + N'|' + 
	   CAST(cols.is_nullable AS nvarchar(3)) + N'|' + 
	   CAST(cols.is_identity AS nvarchar(3))+ N'|' + 
	   CAST(cols.is_computed AS nvarchar(3)) AS FullColumnName
FROM sys.columns cols
INNER join sys.tables tbls ON cols.object_id = tbls.object_id
INNER join sys.schemas sch ON sch.schema_id = tbls.schema_id
INNER join sys.types typ ON typ.user_type_id = cols.user_type_id
WHERE tbls.type = 'U'
AND sch.name IN ({SchemaNamesAsString})
ORDER BY sch.name, tbls.name, cols.column_id
";

        public CalculateDatabaseHashTask() { }

        public CalculateDatabaseHashTask(List<string> schemaNames)
            : this()
        {
            SchemaNames = schemaNames;
        }

        public CalculateDatabaseHashTask Calculate()
        {
            Execute();
            return this;
        }

        public static string Calculate(List<string> schemaNames) =>
            new CalculateDatabaseHashTask(schemaNames).Calculate().DatabaseHash;

        public static string Calculate(
            IConnectionManager connectionManager,
            List<string> schemaNames
        ) =>
            new CalculateDatabaseHashTask(schemaNames) { ConnectionManager = connectionManager }
                .Calculate()
                .DatabaseHash;
    }
}
