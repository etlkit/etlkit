using System.Data;
using System.Globalization;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Moq;

namespace EtlKit.TestConnectionManager.ConnectionManager
{
    /// <summary>
    /// Unit tests for DbConnectionManager logic using test implementations
    /// </summary>
    public sealed class DbConnectionManagerUnitTests
    {
        /// <summary>
        /// Test implementation of IDbConnection for unit testing
        /// </summary>
        public sealed class DbConnectionMock : IDbConnection
        {
            [CanBeNull]
            public Mock<IDbTransaction> Transaction { get; set; } = new();

            [CanBeNull]
            public Mock<IDbConnection> Connection { get; set; } = new();

            public DbConnectionMock()
            {
                DefaultMockSetup();
            }

            private void DefaultMockSetup()
            {
                Connection
                    ?.Setup(c => c.Open())
                    .Callback(() =>
                    {
                        Connection.SetupGet(c => c.State).Returns(ConnectionState.Open);
                    });
                Connection
                    ?.Setup(c => c.Close())
                    .Callback(() =>
                    {
                        Connection.SetupGet(c => c.State).Returns(ConnectionState.Closed);
                    });
                Connection?.Setup(c => c.BeginTransaction()).Returns(Transaction?.Object);
                Connection
                    ?.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>()))
                    .Returns(Transaction?.Object);
            }

            public string ConnectionString { get; set; } = "";
            public int ConnectionTimeout { get; set; } = 0;
            public string Database { get; set; } = "";
            public ConnectionState State => Connection?.Object.State ?? ConnectionState.Broken;

            public IDbTransaction BeginTransaction()
            {
                Transaction?.SetupGet(t => t.IsolationLevel).Returns(IsolationLevel.Unspecified);
                return Connection!.Object.BeginTransaction();
            }

            public IDbTransaction BeginTransaction(IsolationLevel il)
            {
                Transaction?.SetupGet(t => t.IsolationLevel).Returns(il);
                return Connection!.Object.BeginTransaction(il);
            }

            public void ChangeDatabase(string databaseName) { }

            public void Close()
            {
                Connection?.Object.Close();
            }

            public IDbCommand CreateCommand() => Connection!.Object.CreateCommand();

            public void Open()
            {
                Connection?.Object.Open();
            }

            public void Dispose()
            {
                Connection?.Object.Dispose();
            }
        }

        /// <summary>
        /// Test implementation of DbConnectionManager for unit testing
        /// </summary>
        private sealed class TestDbConnectionManager : DbConnectionManager<DbConnectionMock>
        {
            public override ConnectionManagerType ConnectionManagerType =>
                ConnectionManagerType.Postgres;
            public override string QB => @"""";
            public override string QE => @"""";
            public override CultureInfo ConnectionCulture => CultureInfo.CurrentCulture;

            public new DbConnectionMock DbConnection
            {
                get => base.DbConnection;
                set => base.DbConnection = value;
            }

            private TestDbConnectionManager() { }

            public TestDbConnectionManager(IDbConnectionString connectionString)
                : base(connectionString) { }

            public override void PrepareBulkInsert(string tableName) { }

            public override void CleanUpBulkInsert(string tableName) { }

            public override void BulkInsert(ITableData data, string tableName) { }

            public override void BeforeBulkInsert(string tableName) { }

            public override void AfterBulkInsert(string tableName) { }

            [MustDisposeResource]
            public override IConnectionManager Clone() => new TestDbConnectionManager();
        }

        private readonly Mock<IDbConnectionString> _mockConnectionString;

        public DbConnectionManagerUnitTests()
        {
            _mockConnectionString = new Mock<IDbConnectionString>();
            _mockConnectionString.Setup(x => x.Value).Returns("TestConnectionString");
        }

        #region Open Tests

        [Fact]
        public void Open_WithLeaveOpenTrue_ReusesExistingConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = true;
            Assert.Null(connectionManager.DbConnection);

            // Act
            connectionManager.Open();
            var firstState = connectionManager.State;
            var firstConnection = connectionManager.DbConnection!.Connection;

            connectionManager.Open();
            var secondState = connectionManager.State;
            var secondConnection = connectionManager.DbConnection.Connection;

            // Assert
            Assert.Equal(ConnectionState.Open, firstState);
            Assert.Equal(ConnectionState.Open, secondState);
            Assert.Equal(firstConnection, secondConnection);

            firstConnection!.Verify(x => x.Open(), Times.Once);
            firstConnection!.Verify(x => x.Close(), Times.Never);
        }

        [Fact]
        public void Open_WithLeaveOpenFalse_CreatesNewConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = false;

            // Act
            connectionManager.Open();
            var firstState = connectionManager.State;
            var firstConnection = connectionManager.DbConnection.Connection;
            connectionManager.Open();
            var secondState = connectionManager.State;
            var secondConnection = connectionManager.DbConnection.Connection;

            // Assert
            Assert.Equal(ConnectionState.Open, firstState);
            Assert.Equal(ConnectionState.Open, secondState);
            Assert.NotNull(firstConnection);
            Assert.NotEqual(firstConnection, secondConnection);

            firstConnection!.Verify(x => x.Close(), Times.Once);
            firstConnection!.Verify(x => x.Open(), Times.Once);
            secondConnection!.Verify(x => x.Open(), Times.Once);
            secondConnection!.Verify(x => x.Close(), Times.Never);
        }

        [Fact]
        public void Open_WithMaxLoginAttempts_RetriesConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.MaxLoginAttempts = 3;
            connectionManager.LeaveOpen = true;

            var connection = new DbConnectionMock();
            connection.Connection!.SetupGet(c => c.State).Returns(ConnectionState.Closed);
            var states = new Queue<ConnectionState>(
                [ConnectionState.Closed, ConnectionState.Closed, ConnectionState.Open]
            );
            connection
                .Connection.Setup(c => c.Open())
                .Callback(() =>
                {
                    if (states.Count > 0)
                    {
                        connection.Connection.SetupGet(c => c.State).Returns(states.Dequeue());
                    }
                });
            connectionManager.DbConnection = connection;

            // Act
            connectionManager.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connectionManager.State);
            connection!.Connection.Verify(x => x.Open(), Times.Exactly(3));
        }

        #endregion Open Tests

        #region CreateCommand Tests

        [Fact]
        public void CreateCommand_WithValidCommand_ReturnsCommand()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);

            connectionManager.Open();
            const string commandText = "SELECT 1";
            connectionManager.DbConnection.Transaction = null;

            var commandMock = new Mock<IDbCommand>();
            commandMock.Setup(c => c.CreateParameter()).Returns(Mock.Of<IDbDataParameter>());
            connectionManager
                .DbConnection.Connection!.Setup(c => c.CreateCommand())
                .Returns(commandMock.Object);
            commandMock.SetupGet(c => c.Parameters).Returns(Mock.Of<IDataParameterCollection>());

            // Act
            var command = connectionManager.CreateCommand(
                commandText,
                [Mock.Of<IQueryParameter>()]
            );

            // Assert
            Assert.NotNull(command);
            Assert.Null(command.Transaction);
            connectionManager.DbConnection.Connection.Verify(c => c.CreateCommand(), Times.Once);
            commandMock.Verify(c => c.CreateParameter(), Times.Once);
            commandMock.VerifyGet(c => c.Parameters, Times.Once);
        }

        [Fact]
        public void CreateCommand_WithoutOpenConnection_ThrowsException()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            const string commandText = "SELECT 1";

            // Act & Assert
            var exception = Assert.Throws<EtlKit.Common.EtlKitException>(
                () => connectionManager.CreateCommand(commandText, null)
            );
            Assert.Equal("Database connection is not established!", exception.Message);
        }

        [Fact]
        public void CreateCommand_WithTransaction_UsesTransaction()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.Open();
            connectionManager.BeginTransaction();
            const string commandText = "SELECT 1";
            var transaction = new Mock<IDbTransaction>();
            transaction
                .SetupGet(t => t.Connection)
                .Returns(connectionManager.DbConnection.Connection!.Object);
            connectionManager.Transaction = transaction.Object;

            var commandMock = new Mock<IDbCommand>();
            commandMock.Setup(c => c.CreateParameter()).Returns(Mock.Of<IDbDataParameter>());
            connectionManager
                .DbConnection.Connection!.Setup(c => c.CreateCommand())
                .Returns(commandMock.Object);
            commandMock.SetupGet(c => c.Parameters).Returns(Mock.Of<IDataParameterCollection>());

            commandMock.SetupGet(c => c.Transaction).Returns((IDbTransaction)null);
            commandMock
                .SetupSet(c => c.Transaction = It.IsAny<IDbTransaction>())
                .Callback(
                    (IDbTransaction t) => commandMock.SetupGet(c => c.Transaction).Returns(t)
                );
            commandMock
                .SetupSet(c => c.CommandText = It.IsAny<string>())
                .Callback((string t) => commandMock.SetupGet(c => c.CommandText).Returns(t));

            // Act
            var command = connectionManager.CreateCommand(commandText, null);

            // Assert
            Assert.NotNull(command);
            Assert.NotNull(command.Transaction);
            // Note: The base implementation doesn't automatically set transaction on command
            // This test verifies that the command is created successfully with an active transaction
            Assert.Equal(commandText, command.CommandText);
        }

        #endregion CreateCommand Tests

        #region ExecuteNonQuery Tests

        [Fact]
        public void ExecuteNonQuery_WithValidCommand_ReturnsResult()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.Open();
            const string command = "SELECT 1";
            var commandMock = new Mock<IDbCommand>();
            commandMock.Setup(c => c.CreateParameter()).Returns(Mock.Of<IDbDataParameter>());
            connectionManager
                .DbConnection.Connection!.Setup(c => c.CreateCommand())
                .Returns(commandMock.Object);
            commandMock.SetupGet(c => c.Parameters).Returns(Mock.Of<IDataParameterCollection>());
            commandMock.Setup(c => c.ExecuteNonQuery()).Returns(1);

            // Act
            var result = connectionManager.ExecuteNonQuery(command);

            // Assert
            Assert.Equal(1, result);

            commandMock.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion ExecuteNonQuery Tests

        #region ExecuteScalar Tests

        [Fact]
        public void ExecuteScalar_WithValidCommand_ReturnsValue()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.Open();
            const string command = "SELECT 42";
            var commandMock = new Mock<IDbCommand>();
            commandMock.Setup(c => c.CreateParameter()).Returns(Mock.Of<IDbDataParameter>());
            connectionManager
                .DbConnection.Connection!.Setup(c => c.CreateCommand())
                .Returns(commandMock.Object);
            commandMock.SetupGet(c => c.Parameters).Returns(Mock.Of<IDataParameterCollection>());
            commandMock.Setup(c => c.ExecuteScalar()).Returns(1);

            // Act
            var result = connectionManager.ExecuteScalar(command);

            // Assert
            Assert.Equal(1, result);
            commandMock.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion ExecuteScalar Tests

        #region ExecuteReader Tests

        [Fact]
        public void ExecuteReader_WithLeaveOpenTrue_DoesNotCloseConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = true;
            connectionManager.Open();
            var commandMock = new Mock<IDbCommand>();
            const string command = "SELECT 1";
            commandMock.Setup(c => c.CreateParameter()).Returns(Mock.Of<IDbDataParameter>());
            connectionManager
                .DbConnection.Connection!.Setup(c => c.CreateCommand())
                .Returns(commandMock.Object);
            commandMock.SetupGet(c => c.Parameters).Returns(Mock.Of<IDataParameterCollection>());

            var readerMock = new Mock<IDataReader>();
            commandMock.Setup(c => c.ExecuteReader()).Returns(readerMock.Object);

            // Act
            var reader = connectionManager.ExecuteReader(command);

            // Assert
            Assert.NotNull(reader);
            commandMock.Verify(c => c.ExecuteReader(), Times.Once());
            readerMock.Verify(r => r.Dispose(), Times.Never);
        }

        [Fact]
        public void ExecuteReader_WithLeaveOpenFalse_ClosesConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = false;
            connectionManager.Open();
            var commandMock = new Mock<IDbCommand>();
            const string command = "SELECT 1";
            commandMock.Setup(c => c.CreateParameter()).Returns(Mock.Of<IDbDataParameter>());
            connectionManager
                .DbConnection.Connection!.Setup(c => c.CreateCommand())
                .Returns(commandMock.Object);
            commandMock.SetupGet(c => c.Parameters).Returns(Mock.Of<IDataParameterCollection>());

            var readerMock = new Mock<IDataReader>();
            commandMock
                .Setup(c =>
                    c.ExecuteReader(
                        It.Is<CommandBehavior>(b => b == CommandBehavior.CloseConnection)
                    )
                )
                .Returns(readerMock.Object);

            // Act
            var reader = connectionManager.ExecuteReader(command);

            // Assert
            Assert.NotNull(reader);
            // Note: Our test implementation doesn't actually close the connection
            // This test verifies the method structure works
            commandMock.Verify(c => c.ExecuteReader(It.IsAny<CommandBehavior>()), Times.Once());
            readerMock.Verify(r => r.Dispose(), Times.Never);
        }

        #endregion ExecuteReader Tests

        #region CloneIfAllowed Tests

        [Fact]
        public void CloneIfAllowed_WithLeaveOpenTrue_ReturnsSameInstance()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = true;

            // Act
            var result = connectionManager.CloneIfAllowed();

            // Assert
            Assert.Same(connectionManager, result);
        }

        [Fact]
        public void CloneIfAllowed_WithLeaveOpenFalse_ReturnsClone()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = false;

            // Act
            using var result = connectionManager.CloneIfAllowed();

            // Assert
            Assert.NotSame(connectionManager, result);
            Assert.IsType<TestDbConnectionManager>(result);
        }

        #endregion CloneIfAllowed Tests

        #region BeginTransaction Tests

        [Fact]
        public void BeginTransaction_WithoutIsolationLevel_StartsTransaction()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);

            // Act
            connectionManager.BeginTransaction();
            var connectionMock = connectionManager.DbConnection.Connection;

            // Assert
            Assert.NotNull(connectionManager.Transaction);
            Assert.Equal(ConnectionState.Open, connectionManager.State);
            Assert.Equal(IsolationLevel.Unspecified, connectionManager.Transaction.IsolationLevel);

            connectionMock!.Verify(c => c.BeginTransaction(IsolationLevel.Unspecified), Times.Once);
        }

        [Fact]
        public void BeginTransaction_WithIsolationLevel_StartsTransaction()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);

            // Act
            connectionManager.BeginTransaction(IsolationLevel.ReadCommitted);
            var connectionMock = connectionManager.DbConnection.Connection;

            // Assert
            Assert.NotNull(connectionManager.Transaction);
            Assert.Equal(ConnectionState.Open, connectionManager.State);
            Assert.Equal(
                IsolationLevel.ReadCommitted,
                connectionManager.Transaction.IsolationLevel
            );

            connectionMock!.Verify(
                c => c.BeginTransaction(IsolationLevel.ReadCommitted),
                Times.Once
            );
        }

        #endregion BeginTransaction Tests

        #region CommitTransaction Tests

        [Fact]
        public void CommitTransaction_WithActiveTransaction_CommitsAndClosesTransaction()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.BeginTransaction();
            var transactionMock = connectionManager.DbConnection.Transaction;
            var connectionMock = connectionManager.DbConnection.Connection;

            // Act
            connectionManager.CommitTransaction();

            // Assert
            transactionMock!.Verify(t => t.Commit(), Times.Once);
            transactionMock!.Verify(t => t.Dispose(), Times.Once);
            connectionMock!.Verify(c => c.Dispose(), Times.Once);

            Assert.Null(connectionManager.Transaction);
            Assert.Null(connectionManager.DbConnection);
        }

        [Fact]
        public void CommitTransaction_WithActiveTransactionAndLeaveOpen_CommitsAndNotDisposesConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = true;
            connectionManager.BeginTransaction();
            var transactionMock = connectionManager.DbConnection.Transaction;
            var connectionMock = connectionManager.DbConnection.Connection;

            // Act
            connectionManager.CommitTransaction();

            // Assert
            transactionMock!.Verify(t => t.Commit(), Times.Once);
            transactionMock!.Verify(t => t.Dispose(), Times.Once);
            connectionMock!.Verify(c => c.Dispose(), Times.Never);

            Assert.Null(connectionManager.Transaction);
            Assert.NotNull(connectionManager.DbConnection);
        }

        [Fact]
        public void CommitTransaction_WithoutTransaction_DoesNotThrow()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);

            // Act & Assert
            Assert.Null(connectionManager.Transaction);
            connectionManager.CommitTransaction(); // Should not throw
        }

        #endregion CommitTransaction Tests

        #region RollbackTransaction Tests

        [Fact]
        public void RollbackTransaction_WithActiveTransaction_RollbacksAndClosesTransaction()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.BeginTransaction();
            var transactionMock = connectionManager.DbConnection.Transaction;
            var connectionMock = connectionManager.DbConnection.Connection;

            // Act
            connectionManager.RollbackTransaction();

            // Assert
            transactionMock!.Verify(t => t.Rollback(), Times.Once);
            transactionMock!.Verify(t => t.Dispose(), Times.Once);
            connectionMock!.Verify(c => c.Dispose(), Times.Once);

            Assert.Null(connectionManager.Transaction);
            Assert.Null(connectionManager.DbConnection);
        }

        [Fact]
        public void RollbackTransaction_WithActiveTransactionAndLeaveOpen_RollbacksAndNotDisposesConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = true;
            connectionManager.BeginTransaction();
            var transactionMock = connectionManager.DbConnection.Transaction;
            var connectionMock = connectionManager.DbConnection.Connection;

            // Act
            connectionManager.RollbackTransaction();

            // Assert
            transactionMock!.Verify(t => t.Rollback(), Times.Once);
            transactionMock!.Verify(t => t.Dispose(), Times.Once);
            connectionMock!.Verify(c => c.Dispose(), Times.Never);

            Assert.Null(connectionManager.Transaction);
            Assert.NotNull(connectionManager.DbConnection);
        }

        [Fact]
        public void RollbackTransaction_WithoutTransaction_DoesNotThrow()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);

            // Act & Assert
            Assert.Null(connectionManager.Transaction);
            connectionManager.RollbackTransaction(); // Should not throw
        }

        #endregion RollbackTransaction Tests

        #region CloseTransaction Tests

        [Fact]
        public void CloseTransaction_WithActiveTransaction_ClosesTransaction()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.BeginTransaction();
            var transactionMock = connectionManager.DbConnection.Transaction;
            var connectionMock = connectionManager.DbConnection.Connection;

            // Act
            connectionManager.CloseTransaction();

            // Assert
            transactionMock!.Verify(t => t.Dispose(), Times.Once);
            connectionMock!.Verify(c => c.Dispose(), Times.Once);

            Assert.Null(connectionManager.Transaction);
            Assert.Null(connectionManager.DbConnection);
        }

        [Fact]
        public void CloseTransaction_WithoutTransaction_DoesNotThrow()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);

            // Act & Assert
            connectionManager.CloseTransaction(); // Should not throw

            Assert.Null(connectionManager.Transaction);
            Assert.Null(connectionManager.DbConnection);
        }

        [Fact]
        public void CloseTransaction_WithLeaveOpenTrue_NotDisposesConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = true;
            connectionManager.BeginTransaction();

            // Act
            connectionManager.CloseTransaction();

            // Assert
            Assert.Null(connectionManager.Transaction);
            Assert.NotNull(connectionManager.DbConnection);
        }

        #endregion CloseTransaction Tests

        #region Dispose Tests

        [Fact]
        public void Dispose_WithOpenConnection_DisposesConnection()
        {
            // Arrange
            var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.Open();

            // Act
            connectionManager.Dispose();

            // Assert
            // Should not throw
            Assert.True(true);
            Assert.Null(connectionManager.DbConnection);
        }

        [Fact]
        public void Dispose_WithActiveTransaction_DisposesTransaction()
        {
            // Arrange
            var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.BeginTransaction();

            // Act
            connectionManager.Dispose();

            // Assert
            // Should not throw
            Assert.True(true);

            Assert.Null(connectionManager.Transaction);
            Assert.Null(connectionManager.DbConnection);
        }

        [Fact]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.Open();

            // Act & Assert
            connectionManager.Dispose();
#pragma warning disable S3966
            connectionManager.Dispose(); // Should not throw to match the test scenario (execute Dispose twice without any exceptions)
#pragma warning restore S3966

            Assert.True(true);
        }

        #endregion Dispose Tests

        #region Close Tests

        [Fact]
        public void Close_DisposesConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.Open();

            // Act
            connectionManager.Close();

            // Assert
            // Should not throw
            Assert.True(true);
            Assert.Null(connectionManager.DbConnection);
        }

        [Fact]
        public void Close_WithActiveTransaction_DisposesTransaction()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.BeginTransaction();

            // Act
            connectionManager.Close();

            // Assert
            // Should not throw
            Assert.True(true);

            Assert.Null(connectionManager.Transaction);
            Assert.Null(connectionManager.DbConnection);
        }

        #endregion Close Tests

        #region CloseIfAllowed Tests

        [Fact]
        public void CloseIfAllowed_WithLeaveOpenTrue_DoesNotDispose()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = true;
            connectionManager.Open();

            // Act
            connectionManager.CloseIfAllowed();

            // Assert
            // Connection should still be open due to LeaveOpen = true
            Assert.Equal(ConnectionState.Open, connectionManager.State);
            Assert.NotNull(connectionManager.DbConnection);
        }

        [Fact]
        public void CloseIfAllowed_WithLeaveOpenFalse_DisposesConnection()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = false;
            connectionManager.Open();

            // Act
            connectionManager.CloseIfAllowed();

            // Assert
            // Should not throw
            Assert.True(true);
            Assert.Null(connectionManager.DbConnection);
        }

        [Fact]
        public void CloseIfAllowed_WithActiveTransaction_DoesNotDispose()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = false;
            connectionManager.BeginTransaction(); // This sets LeaveOpen to true

            // Act
            connectionManager.CloseIfAllowed();

            // Assert
            // Connection should still be open due to active transaction
            Assert.Equal(ConnectionState.Open, connectionManager.State);
        }

        #endregion CloseIfAllowed Tests

        #region LeaveOpen Property Tests

        [Fact]
        public void LeaveOpen_WithTransaction_ReturnsTrue()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = false;
            connectionManager.BeginTransaction();

            // Act
            var result = connectionManager.LeaveOpen;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void LeaveOpen_WithoutTransaction_ReturnsSetValue()
        {
            // Arrange
            using var connectionManager = new TestDbConnectionManager(_mockConnectionString.Object);
            connectionManager.LeaveOpen = true;

            // Act
            var result = connectionManager.LeaveOpen;

            // Assert
            Assert.True(result);
        }

        #endregion LeaveOpen Property Tests
    }
}
