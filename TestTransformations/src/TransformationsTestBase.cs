using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using EtlKit.TestShared.Helper;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations
{
    [CollectionDefinition("Transformations")]
    public class DataFlowCollection : ICollectionFixture<TransformationsDatabaseFixture> { }

    [Collection("Transformations")]
    public class TransformationsTestBase
    {
        protected readonly TransformationsDatabaseFixture Fixture;

        protected TransformationsTestBase(TransformationsDatabaseFixture fixture)
        {
            Fixture = fixture;
        }

        protected static SqlConnectionManager SqlConnection =>
            Config.SqlConnection.ConnectionManager("DataFlow");

        public static TheoryData<IConnectionManager> AllSqlConnections =>
            new(Config.AllSqlConnections("DataFlow"));
    }
}
