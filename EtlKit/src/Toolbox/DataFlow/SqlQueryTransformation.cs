using System.Linq;
using DotLiquid;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Sql Query Transformation accepts a SQL statement that is executed for each input row.
    /// The Sql statement can contain liquid placeholders, parameters for which are expected
    /// from input source. Executes SQL query for each input row and provides all returned
    /// result rows to Output.
    /// </summary>
    /// <typeparam name="TInput">Input source type, providing parameters for Sql template</typeparam>
    /// <typeparam name="TOutput">Destination type, one or multiple rows for each input</typeparam>
    public class SqlQueryTransformation<TInput, TOutput> : RowMultiplication<TInput, TOutput>
    {
        public SqlQueryTransformation()
        {
            MultiplicationFunc = obj =>
            {
                var dbSource = new DbSource<TOutput>(ConnectionManager)
                {
                    SourceTableDefinition = SourceTableDefinition,
                    ColumnNames = ColumnNames
                };
                var sql = TransformParameters(obj);
                dbSource.Sql = sql;
                var dest = new MemoryDestination<TOutput>();
                dbSource.LinkTo(dest);
                dbSource.Execute();
                dest.Wait();
                return dest.Data;
            };
        }

        /// <summary>
        /// Sql query columns definition for an internal <see cref="DbSource" /> object running Sql query
        /// </summary>
        public TableDefinition SourceTableDefinition { get; set; }

        /// <summary>
        /// Column list definition for an internal <see cref="DbSource" /> object running Sql query
        /// </summary>
        public List<string> ColumnNames { get; set; }

        /// <summary>
        /// Sql liquid template <a href="https://shopify.github.io/liquid/"/>
        /// </summary>
        public string SqlTemplate { get; set; }

#pragma warning disable S2376
        // ReSharper disable once UnusedMember.Global
        // ReSharper disable once InconsistentNaming
        [Obsolete($"use {nameof(SqlTemplate)}")]
        public string SQLTemplate
        {
            set { SqlTemplate = value; }
        }
#pragma warning restore S2376

        /// <summary>
        /// Sql template transformation function
        /// </summary>
        /// <param name="input">input object</param>
        /// <returns>sql query</returns>
        protected virtual string TransformParameters(TInput input)
        {
            var templateSql = Template.Parse(SqlTemplate);
            var inputDictionary =
                input as IDictionary<string, object>
                ?? input
                    .GetType()
                    .GetProperties()
                    .ToDictionary(p => p.Name, p => p.GetValue(input));
            var resultQuery = templateSql.Render(Hash.FromDictionary(inputDictionary));

            return resultQuery;
        }
    }

    /// <summary>
    /// Non-generic for <see cref="SqlQueryTransformation&lt;TInput,TOutput&gt;" />, working with dynamic objects
    /// </summary>
    public class SqlQueryTransformation : SqlQueryTransformation<ExpandoObject, ExpandoObject>
    {
        protected override string TransformParameters(ExpandoObject obj)
        {
            var templateSql = Template.Parse(SqlTemplate);
            var resultQuery = templateSql.Render(Hash.FromDictionary(obj));

            return resultQuery;
        }
    };
}
