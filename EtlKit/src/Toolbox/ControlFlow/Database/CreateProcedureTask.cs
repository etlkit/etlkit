using System.Linq;

using EtlKit.Primitives;

using EtlKit.Common;
using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// Creates or updates a procedure.
    /// </summary>
    /// <example>
    /// <code>
    /// CRUDProcedureTask.CreateOrAlter("demo.proc1", "select 1 as test");
    /// </code>
    /// </example>
    [PublicAPI]
    public class CreateProcedureTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => $"{CreateOrAlterSql} procedure {ProcedureName}";

        public void Execute()
        {
            if (!DbConnectionManager.SupportProcedures)
                throw new ETLBoxNotSupportedException("This task is not supported!");

            IsExisting = new IfProcedureExistsTask(ProcedureName)
            {
                ConnectionManager = ConnectionManager,
                DisableLogging = true
            }.Exists();
            if (IsExisting && ConnectionType == ConnectionManagerType.MySql)
                new DropProcedureTask(ProcedureName)
                {
                    ConnectionManager = ConnectionManager,
                    DisableLogging = true
                }.Drop();
            new SqlTask(this, Sql).ExecuteNonQuery();
        }

        /* Public properties */
        public string ProcedureName { get; set; }
        public ObjectNameDescriptor PN => new(ProcedureName, QB, QE);
        public string ProcedureDefinition { get; set; }
        public IList<ProcedureParameter> ProcedureParameters { get; set; }
        public string Sql =>
            $@"{CreateOrAlterSql} PROCEDURE {PN.QuotedFullName}{ParameterDefinition}{Language}
{As}
{Begin}

{ProcedureDefinition}

{End}
        ";

        public CreateProcedureTask() { }

        public CreateProcedureTask(string procedureName, string procedureDefinition)
            : this()
        {
            ProcedureName = procedureName;
            ProcedureDefinition = procedureDefinition;
        }

        public CreateProcedureTask(
            string procedureName,
            string procedureDefinition,
            IList<ProcedureParameter> procedureParameter
        )
            : this(procedureName, procedureDefinition)
        {
            ProcedureParameters = procedureParameter;
        }

        public CreateProcedureTask(ProcedureDefinition definition)
            : this()
        {
            ProcedureName = definition.Name;
            ProcedureDefinition = definition.Definition;
            ProcedureParameters = definition.Parameter;
        }

        public static void CreateOrAlter(string procedureName, string procedureDefinition) =>
            new CreateProcedureTask(procedureName, procedureDefinition).Execute();

        public static void CreateOrAlter(
            string procedureName,
            string procedureDefinition,
            IList<ProcedureParameter> procedureParameter
        ) =>
            new CreateProcedureTask(
                procedureName,
                procedureDefinition,
                procedureParameter
            ).Execute();

        public static void CreateOrAlter(ProcedureDefinition procedure) =>
            new CreateProcedureTask(procedure).Execute();

        public static void CreateOrAlter(
            IConnectionManager connectionManager,
            string procedureName,
            string procedureDefinition
        ) =>
            new CreateProcedureTask(procedureName, procedureDefinition)
            {
                ConnectionManager = connectionManager
            }.Execute();

        public static void CreateOrAlter(
            IConnectionManager connectionManager,
            string procedureName,
            string procedureDefinition,
            IList<ProcedureParameter> procedureParameter
        ) =>
            new CreateProcedureTask(procedureName, procedureDefinition, procedureParameter)
            {
                ConnectionManager = connectionManager
            }.Execute();

        public static void CreateOrAlter(
            IConnectionManager connectionManager,
            ProcedureDefinition procedure
        ) => new CreateProcedureTask(procedure) { ConnectionManager = connectionManager }.Execute();

        private bool IsExisting { get; set; }

        private string CreateOrAlterSql
        {
            get
            {
                return ConnectionType switch
                {
                    ConnectionManagerType.Postgres => "CREATE OR REPLACE",
                    ConnectionManagerType.MySql => "CREATE",
                    _ => IsExisting ? "ALTER" : "CREATE"
                };
            }
        }

        private string ParameterDefinition
        {
            get
            {
                var result = "";
                if (ConnectionType is ConnectionManagerType.Postgres or ConnectionManagerType.MySql)
                    result += "(";
                result +=
                    ProcedureParameters?.Count > 0
                        ? string.Join(",", ProcedureParameters.Select(ParameterSql))
                        : string.Empty;
                if (ConnectionType is ConnectionManagerType.Postgres or ConnectionManagerType.MySql)
                    result += ")";
                return result;
            }
        }

        public string ParameterSql(ProcedureParameter par)
        {
            var sql = Environment.NewLine + "";
            if (ConnectionType == ConnectionManagerType.SqlServer)
                sql += "@";
            if (ConnectionType == ConnectionManagerType.MySql)
                sql += par.Out ? "OUT " : "IN ";
            sql += $@"{par.Name} {par.DataType}";
            if (par.HasDefaultValue && ConnectionType != ConnectionManagerType.MySql)
                sql += $" = {par.DefaultValue}";
            if (par.Out && ConnectionType != ConnectionManagerType.MySql)
                sql += " OUT";
            if (par.ReadOnly)
                sql += " READONLY";
            return sql;
        }

        private string Language =>
            ConnectionType == ConnectionManagerType.Postgres
                ? Environment.NewLine + "LANGUAGE SQL"
                : "";

        private string Begin => ConnectionType == ConnectionManagerType.Postgres ? "$$" : "BEGIN";
        private string End => ConnectionType == ConnectionManagerType.Postgres ? "$$" : "END";
        private string As => ConnectionType == ConnectionManagerType.MySql ? "" : "AS";
    }
}
