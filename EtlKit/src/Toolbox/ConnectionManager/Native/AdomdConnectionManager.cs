using EtlKit.Primitives;
using Microsoft.AnalysisServices.AdomdClient;

namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Connection manager for Adomd connection to a sql server analysis server.
    /// </summary>
    /// <example>
    /// <code>
    /// ControlFlow.DefaultDbConnection = new AdmoConnectionManager(new ConnectionString("..connection string.."));
    /// </code>
    /// </example>
    [PublicAPI]
    public class AdomdConnectionManager : DbConnectionManager<AdomdConnection>
    {
        public override ConnectionManagerType ConnectionManagerType { get; } =
            ConnectionManagerType.Adomd;
        public override string QB { get; } = string.Empty;
        public override string QE { get; } = string.Empty;
        public override CultureInfo ConnectionCulture => CultureInfo.CurrentCulture;

        public AdomdConnectionManager() { }

        public AdomdConnectionManager(SqlConnectionString connectionString)
            : base(connectionString) { }

        public AdomdConnectionManager(string connectionString)
            : base(new SqlConnectionString(connectionString)) { }

        public override void BulkInsert(ITableData data, string tableName)
        {
            throw new NotImplementedException();
        }

        public override void PrepareBulkInsert(string tableName) { }

        public override void CleanUpBulkInsert(string tableName) { }

        public override void BeforeBulkInsert(string tableName) { }

        public override void AfterBulkInsert(string tableName) { }

        public override IConnectionManager Clone()
        {
            var clone = new AdomdConnectionManager(
                (SqlConnectionString)ConnectionString
            )
            {
                MaxLoginAttempts = MaxLoginAttempts
            };
            return clone;
        }
    }
}
