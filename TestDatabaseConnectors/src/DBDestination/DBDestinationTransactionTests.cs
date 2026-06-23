using System.Data;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;
using EtlKit.TestShared;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationTransactionTests : DatabaseConnectorsTestBase
    {
        public DbDestinationTransactionTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;
        public static IEnumerable<object[]> ConnectionsWithPK => AllSqlConnectionsWithPK;

        [Theory]
        [MemberData(nameof(ConnectionsWithPK))]
        public void ErrorInBatch(ConnectionManagerWithPK data)
        {
            //Arrange
            _ = new TwoColumnsTableFixture(data.Connection, "TransactionDest", data.WithPK);
            var source = new MemorySource<MySimpleRow>
            {
                DataAsList = new List<MySimpleRow>
                {
                    new() { Col1 = "1", Col2 = "Test1" },
                    new() { Col1 = "2", Col2 = "Test2" },
                    new() { Col1 = null, Col2 = "Test3" },
                },
            };
            var dest = new DbDestination<MySimpleRow>(data.Connection, "TransactionDest", 2);

            //Act & Assert
            source.LinkTo(dest);

            // ClickHouse writes a default values in not-null columns
            if (data.Connection.ConnectionManagerType == ConnectionManagerType.ClickHouse)
            {
                source.Execute();
                dest.Wait();
                Assert.Equal(3, RowCountTask.Count(data.Connection, "TransactionDest"));
            }
            else
            {
                Assert.ThrowsAny<Exception>(() =>
                {
                    source.Execute();
                    dest.Wait();
                });

                Assert.Equal(2, RowCountTask.Count(data.Connection, "TransactionDest"));
            }

            //Assert
            Assert.Null(dest.BulkInsertConnectionManager?.State);
            Assert.True(data.Connection.State == null);
        }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutClickHouseWithPK))]
        public void CloseConnectionDuringTransaction(ConnectionManagerWithPK data)
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture(data.Connection, "TransactionSource", data.WithPK);
            s2C.InsertTestData();
            var source = new DbSource<MySimpleRow>(data.Connection, "TransactionSource");

            var destinationConnection = data.Connection.Clone();
            var _ = new TwoColumnsTableFixture(destinationConnection, "TransactionDest");
            var dest = new DbDestination<MySimpleRow>(destinationConnection, "TransactionDest", 2);

            //Act & Assert
            destinationConnection.BeginTransaction();
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();
            Assert.Equal(3, RowCountTask.Count(destinationConnection, "TransactionDest"));
            destinationConnection.Close();
            Assert.Equal(0, RowCountTask.Count(destinationConnection, "TransactionDest"));

            //Assert Connections are closed
            Assert.Null(dest.BulkInsertConnectionManager?.State);
            Assert.True(data.Connection.State == null);
            Assert.True(destinationConnection.State == null);
        }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutClickHouse))]
        public void CommitTransaction(IConnectionManager connection)
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture(connection, "TransactionSource");
            s2C.InsertTestData();
            var _ = new TwoColumnsTableFixture(connection, "TransactionDest");
            var source = new DbSource<MySimpleRow>(connection, "TransactionSource");
            var dest = new DbDestination<MySimpleRow>(connection, "TransactionDest", 2);

            //Act & Assert
            connection.BeginTransaction(IsolationLevel.ReadCommitted);
            source.LinkTo(dest);

            source.Execute();
            dest.Wait();

            //Assert
            if (connection.GetType() == typeof(SqlConnectionManager))
                Assert.Equal(
                    3,
                    RowCountTask.Count(
                        connection.Clone(),
                        "TransactionDest",
                        RowCountOptions.NoLock
                    )
                );
            connection.CommitTransaction();
            Assert.Equal(3, RowCountTask.Count(connection, "TransactionDest"));
            Assert.Equal(3, RowCountTask.Count(connection.Clone(), "TransactionDest"));

            //Assert Connections are closed
            Assert.Null(dest.BulkInsertConnectionManager?.State);
            Assert.True(connection.State == null);
        }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutClickHouse))]
        public void RollbackTransaction(IConnectionManager sourceConnection)
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture(sourceConnection, "TransactionSource");
            s2C.InsertTestData();
            var source = new DbSource<MySimpleRow>(sourceConnection, "TransactionSource");

            var destinationConnection = sourceConnection.Clone();
            var _ = new TwoColumnsTableFixture(destinationConnection, "TransactionDest");
            var dest = new DbDestination<MySimpleRow>(destinationConnection, "TransactionDest", 2);

            //Act & Assert
            destinationConnection.BeginTransaction(IsolationLevel.ReadCommitted);
            source.LinkTo(dest);

            source.Execute();
            dest.Wait();
            Assert.Equal(3, RowCountTask.Count(destinationConnection, "TransactionDest"));
            destinationConnection.RollbackTransaction();
            Assert.Equal(0, RowCountTask.Count(destinationConnection, "TransactionDest"));

            //Assert Connections are closed
            Assert.Null(dest.BulkInsertConnectionManager?.State);
            Assert.True(destinationConnection.State == null);
            Assert.True(sourceConnection.State == null);
        }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutClickHouse))]
        public void LeaveOpen(IConnectionManager connection)
        {
            //Arrange
            var s2C = new TwoColumnsTableFixture(connection, "TransactionSource");
            s2C.InsertTestData();
            var _ = new TwoColumnsTableFixture(connection, "TransactionDest");
            var source = new DbSource<MySimpleRow>(connection, "TransactionSource");
            var dest = new DbDestination<MySimpleRow>(connection, "TransactionDest", 2);

            //Act
            connection.LeaveOpen = true;
            connection.BeginTransaction(IsolationLevel.ReadCommitted);
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();
            connection.CommitTransaction();

            //Assert
            Assert.Equal(connection, dest.BulkInsertConnectionManager);

            //Assert Connections are open
            Assert.Equal(ConnectionState.Open, dest.BulkInsertConnectionManager!.State);
            Assert.True(connection.State == ConnectionState.Open);

            Assert.Equal(3, RowCountTask.Count(connection, "TransactionDest"));
        }

        public class MySimpleRow
        {
            public string Col1 { get; set; }
            public string Col2 { get; set; }
        }
    }
}
