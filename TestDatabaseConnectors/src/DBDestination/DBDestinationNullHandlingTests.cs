using EtlKit.DataFlow;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationNullHandlingTests : DatabaseConnectorsTestBase
    {
        public DbDestinationNullHandlingTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void IgnoreWithObject()
        {
            //Arrange
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                SqlConnection,
                "DestIgnoreNullValues"
            );
            MemorySource<MySimpleRow> source = new MemorySource<MySimpleRow>
            {
                DataAsList = new List<MySimpleRow>
                {
                    null,
                    new() { Col1 = 1, Col2 = "Test1" },
                    null,
                    new() { Col1 = 2, Col2 = "Test2" },
                    new() { Col1 = 3, Col2 = "Test3" },
                    null,
                },
            };
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                SqlConnection,
                "DestIgnoreNullValues"
            );

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            d2C.AssertTestData();
        }

        [Fact]
        public void IgnoreWithStringArray()
        {
            //Arrange
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                SqlConnection,
                "DestIgnoreNullValuesStringArray"
            );
            MemorySource<string[]> source = new MemorySource<string[]>
            {
                DataAsList = new List<string[]>
                {
                    null,
                    new[] { "1", "Test1" },
                    null,
                    new[] { "2", "Test2" },
                    new[] { "3", "Test3" },
                    null,
                },
            };
            DbDestination<string[]> dest = new DbDestination<string[]>(
                SqlConnection,
                "DestIgnoreNullValuesStringArray"
            );

            //Act
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            d2C.AssertTestData();
        }
    }
}
