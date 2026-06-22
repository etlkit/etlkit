using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBSource
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbSourceTableDefinitionTests : DatabaseConnectorsTestBase
    {
        public DbSourceTableDefinitionTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MySimpleRow
        {
            public long Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Theory, MemberData(nameof(Connections))]
        public void WithTableDefinition(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture source2Columns = new TwoColumnsTableFixture(
                connection,
                "Source"
            );
            source2Columns.InsertTestData();
            TwoColumnsTableFixture dest2Columns = new TwoColumnsTableFixture(
                connection,
                "Destination"
            );

            //Act
            DbSource<MySimpleRow> source = new DbSource<MySimpleRow>
            {
                SourceTableDefinition = source2Columns.TableDefinition,
                ConnectionManager = connection,
            };
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>
            {
                DestinationTableDefinition = dest2Columns.TableDefinition,
                ConnectionManager = connection,
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }
    }
}
