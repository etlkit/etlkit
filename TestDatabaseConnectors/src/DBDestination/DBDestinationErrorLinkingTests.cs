using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;
using EtlKit.TestShared;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationErrorLinkingTests : DatabaseConnectorsTestBase
    {
        public DbDestinationErrorLinkingTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MySimpleRow
        {
            public string Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutClickHouseWithPK))]
        public void RedirectBatch(ConnectionManagerWithPK data)
        {
            //Arrange
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                data.Connection,
                "DestLinkError",
                data.WithPK
            );
            MemorySource<MySimpleRow> source = new MemorySource<MySimpleRow>
            {
                DataAsList = new List<MySimpleRow>
                {
                    new() { Col1 = null, Col2 = "ErrorRecord1" },
                    new() { Col1 = "X2", Col2 = "ErrorRecord2" },
                    new() { Col1 = "1", Col2 = "Test1" },
                    new() { Col1 = "2", Col2 = "Test2" },
                    new() { Col1 = "3", Col2 = "Test3 - good, but in error batch" },
                    new() { Col1 = null, Col2 = "ErrorRecord3" },
                    new() { Col1 = "3", Col2 = "Test3" },
                },
            };
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                data.Connection,
                "DestLinkError",
                batchSize: 2
            );
            MemoryDestination<EtlKitError> errorDest = new MemoryDestination<EtlKitError>();

            //Act
            source.LinkTo(dest);
            dest.LinkErrorTo(errorDest);
            source.Execute();
            dest.Wait();
            errorDest.Wait();

            var table = DebugHelper.GetTableData(d2C.TableDefinition, data.Connection);

            //Assert
            d2C.AssertTestData();
            Assert.Collection(
                errorDest.Data,
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    ),
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    )
            );
        }
    }
}
