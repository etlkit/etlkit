using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationMultipleSourcesTests : DatabaseConnectorsTestBase
    {
        public DbDestinationMultipleSourcesTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MySimpleRow
        {
            public long Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Theory, MemberData(nameof(Connections))]
        public void TwoMemSourcesIntoDB(IConnectionManager connection)
        {
            //Arrange
            MemorySource<MySimpleRow> source1 = new MemorySource<MySimpleRow>();
            MemorySource<MySimpleRow> source2 = new MemorySource<MySimpleRow>();
            //Act
            source1.DataAsList = new List<MySimpleRow>
            {
                new() { Col1 = 1, Col2 = "Test1" },
                new() { Col1 = 2, Col2 = "Test2" },
            };
            source2.DataAsList = new List<MySimpleRow>
            {
                new() { Col1 = 3, Col2 = "Test3" },
            };
            TwoColumnsTableFixture dest2Columns = new TwoColumnsTableFixture(
                connection,
                "DBMultipleDestination"
            );

            //Act
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                connection,
                "DBMultipleDestination"
            );

            source1.LinkTo(dest);
            source2.LinkTo(dest);
            source2.Execute();
            source1.Execute();

            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
