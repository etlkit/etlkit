using System.Linq;

using DotLiquid;

using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Sql Command Transformation accepts a SQL statement that is executed for each input row.
    /// The Sql statement can contain liquid placeholders, parameters for which are expected
    /// from input source. Performs non-query execution and returns number of rows affected for each input row.
    /// </summary>
    /// <typeparam name="TInput">Parameters for SQL template</typeparam>
    /// <typeparam name="TOutput">Destination type</typeparam>
    public abstract class SqlCommandTransformation<TInput, TOutput>
        : RowTransformation<TInput, TOutput>
    {
        protected SqlCommandTransformation() =>
            TransformationFunc = obj =>
            {
                var sql = TransformParameters(obj);

                var task = new SqlTask($"{nameof(SqlCommandTransformation)}->Execute", sql)
                {
                    ConnectionManager = ConnectionManager
                };
                var affectedRows = task.ExecuteNonQuery();

                return TransformResult(obj, affectedRows);
            };

        /// <summary>
        /// Sql command template in Liquid syntax
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

        /// <summary>
        /// Transform function type for non-query execution result, accepts input object and number of rows affected
        /// </summary>
        public delegate TOutput TransformResultFunc(TInput input, int affectedRows);

        /// <summary>
        /// Transform function for non-query execution result, accepts input object and number of rows affected
        /// </summary>
        // ReSharper disable once MemberCanBeProtected.Global
        public TransformResultFunc TransformResult { get; set; } = (_, _) => default;
    }

    /// <summary>
    /// Non-generic for <see cref="SqlCommandTransformation&lt;TInput,TOutput&gt;" />, working with dynamic objects
    /// </summary>

    public class SqlCommandTransformation : SqlCommandTransformation<ExpandoObject, ExpandoObject>
    {
        public SqlCommandTransformation()
        {
            TransformResult = (input, _) => input;
        }

        protected override string TransformParameters(ExpandoObject obj)
        {
            var templateSql = Template.Parse(SqlTemplate);
            var resultQuery = templateSql.Render(Hash.FromDictionary(obj));

            return resultQuery;
        }
    };
}
