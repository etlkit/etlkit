using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBSource
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbSourceIdentityColumnTests : DatabaseConnectorsTestBase
    {
        public DbSourceIdentityColumnTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MyPartialRow
        {
            public string Col2 { get; set; }
            public decimal? Col4 { get; set; }
        }

        private static void DataFlowForIdentityColumn(IConnectionManager connection)
        {
            DbSource<MyPartialRow> source = new DbSource<MyPartialRow>(connection, "Source4Cols");
            DbDestination<MyPartialRow> dest = new DbDestination<MyPartialRow>(
                connection,
                "Destination4Cols"
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();
        }

        [Theory, MemberData(nameof(Connections))]
        private void IdentityColumnsAtTheBeginning(IConnectionManager connection)
        {
            //Arrange
            var identityIndex = IsIdentitySupported(connection) ? 0 : -1;
            FourColumnsTableFixture source4Columns = new FourColumnsTableFixture(
                connection,
                "Source4Cols",
                identityColumnIndex: identityIndex
            );
            source4Columns.InsertTestData();
            FourColumnsTableFixture dest4Columns = new FourColumnsTableFixture(
                connection,
                "Destination4Cols",
                identityColumnIndex: identityIndex
            );

            //Act
            DataFlowForIdentityColumn(connection);

            //Assert
            dest4Columns.AssertTestData();
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutClickHouse))]
        private void IdentityColumnInTheMiddle(IConnectionManager connection)
        {
            //Arrange
            FourColumnsTableFixture source4Columns = new FourColumnsTableFixture(
                connection,
                "Source4Cols",
                identityColumnIndex: 1
            );
            source4Columns.InsertTestData();
            FourColumnsTableFixture dest4Columns = new FourColumnsTableFixture(
                connection,
                "Destination4Cols",
                identityColumnIndex: 2
            );

            //Act
            DataFlowForIdentityColumn(connection);

            //Assert
            dest4Columns.AssertTestData();
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutClickHouse))]
        private void IdentityColumnAtTheEnd(IConnectionManager connection)
        {
            //Arrange
            FourColumnsTableFixture source4Columns = new FourColumnsTableFixture(
                connection,
                "Source4Cols",
                identityColumnIndex: 3
            );
            source4Columns.InsertTestData();
            FourColumnsTableFixture dest4Columns = new FourColumnsTableFixture(
                connection,
                "Destination4Cols",
                identityColumnIndex: 3
            );

            //Act
            DataFlowForIdentityColumn(connection);

            //Assert
            dest4Columns.AssertTestData();
        }
    }
}
