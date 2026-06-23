using System.Dynamic;
using System.Threading;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBTransformation
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbTransformationDynamicObjectTests : DatabaseConnectorsTestBase
    {
        public DbTransformationDynamicObjectTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutClickHouse))]
        public void SourceMoreColumnsThanDestination(IConnectionManager connection)
        {
            //Arrange
            var source4Columns = new FourColumnsTableFixture(connection, "SourceDynamic4Cols");
            source4Columns.InsertTestData();
            var dest2Columns = new TwoColumnsTableFixture(connection, "TransformationDynamic2Cols");
            var source = new DbSource<ExpandoObject>(connection, "SourceDynamic4Cols");
            var transformation = new DbRowTransformation<ExpandoObject>(
                connection,
                "TransformationDynamic2Cols"
            );
            var dest = new MemoryDestination<ExpandoObject>();

            //Act

            source.LinkTo(transformation);
            transformation.LinkTo(dest);
            source.Execute(CancellationToken.None);
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
            Assert.Equal(3, dest.Data.Count);
        }
    }
}
