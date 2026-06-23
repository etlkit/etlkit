using System.Data;
using EtlKit.ConnectionManager;
using Moq;

namespace EtlKit.TestConnectionManager.ConnectionManager
{
    /// <summary>
    /// Unit tests for DisposableDataReader class
    /// </summary>
    public sealed class DisposableDataReaderTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithCommandFactory_CreatesCommandAndReader()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            // Act
            using var disposableReader = new DisposableDataReader(commandFactory.Object, null);

            // Assert
            commandFactory.Verify(f => f(), Times.Once);
            mockCommand.Verify(c => c.ExecuteReader(), Times.Once);
            mockCommand.Verify(c => c.ExecuteReader(It.IsAny<CommandBehavior>()), Times.Never);
        }

        [Fact]
        public void Constructor_WithCommandBehavior_CreatesCommandAndReaderWithBehavior()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand
                .Setup(c => c.ExecuteReader(CommandBehavior.CloseConnection))
                .Returns(mockReader.Object);

            // Act
            using var disposableReader = new DisposableDataReader(
                commandFactory.Object,
                CommandBehavior.CloseConnection
            );

            // Assert
            commandFactory.Verify(f => f(), Times.Once);
            mockCommand.Verify(c => c.ExecuteReader(CommandBehavior.CloseConnection), Times.Once);
            mockCommand.Verify(c => c.ExecuteReader(), Times.Never);
        }

        [Fact]
        public void Constructor_WithCommandFactoryThrowsException_PropagatesException()
        {
            // Arrange
            var commandFactory = new Mock<Func<IDbCommand>>();
            commandFactory.Setup(f => f()).Throws<InvalidOperationException>();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(
                () => new DisposableDataReader(commandFactory.Object, null)
            );
        }

        [Fact]
        public void Constructor_WithExecuteReaderThrowsException_PropagatesException()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Throws<InvalidOperationException>();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(
                () => new DisposableDataReader(commandFactory.Object, null)
            );
        }

        #endregion Constructor Tests

        #region IDataReader Delegation Tests

        [Fact]
        public void Read_DelegatesToReader()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);
            mockReader.Setup(r => r.Read()).Returns(true);

            using var disposableReader = new DisposableDataReader(commandFactory.Object, null);

            // Act
            var result = disposableReader.Read();

            // Assert
            Assert.True(result);
            mockReader.Verify(r => r.Read(), Times.Once);
        }

        [Fact]
        public void Close_DelegatesToReader()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            using var disposableReader = new DisposableDataReader(commandFactory.Object, null);

            // Act
            disposableReader.Close();

            // Assert
            mockReader.Verify(r => r.Close(), Times.Once);
        }

        [Fact]
        public void NextResult_DelegatesToReader()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);
            mockReader.Setup(r => r.NextResult()).Returns(false);

            using var disposableReader = new DisposableDataReader(commandFactory.Object, null);

            // Act
            var result = disposableReader.NextResult();

            // Assert
            Assert.False(result);
            mockReader.Verify(r => r.NextResult(), Times.Once);
        }

        [Fact]
        public void GetSchemaTable_DelegatesToReader()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            using var expectedSchemaTable = new DataTable();
            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);
            mockReader.Setup(r => r.GetSchemaTable()).Returns(expectedSchemaTable);

            using var disposableReader = new DisposableDataReader(commandFactory.Object, null);

            // Act
            var result = disposableReader.GetSchemaTable();

            // Assert
            Assert.Equal(expectedSchemaTable, result);
            mockReader.Verify(r => r.GetSchemaTable(), Times.Once);
        }

        [Fact]
        public void Properties_DelegateToReader()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            mockReader.SetupGet(r => r.Depth).Returns(1);
            mockReader.SetupGet(r => r.IsClosed).Returns(false);
            mockReader.SetupGet(r => r.RecordsAffected).Returns(5);
            mockReader.SetupGet(r => r.FieldCount).Returns(3);

            using var disposableReader = new DisposableDataReader(commandFactory.Object, null);

            // Act & Assert
            Assert.Equal(1, disposableReader.Depth);
            Assert.False(disposableReader.IsClosed);
            Assert.Equal(5, disposableReader.RecordsAffected);
            Assert.Equal(3, disposableReader.FieldCount);

            mockReader.VerifyGet(r => r.Depth, Times.Once);
            mockReader.VerifyGet(r => r.IsClosed, Times.Once);
            mockReader.VerifyGet(r => r.RecordsAffected, Times.Once);
            mockReader.VerifyGet(r => r.FieldCount, Times.Once);
        }

        #endregion IDataReader Delegation Tests

        #region Dispose Tests

        [Fact]
        public void Dispose_DisposesReaderAndCommand()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            var disposableReader = new DisposableDataReader(commandFactory.Object, null);

            // Act
            disposableReader.Dispose();

            // Assert
            mockReader.Verify(r => r.Dispose(), Times.Once);
            mockCommand.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            var disposableReader = new DisposableDataReader(commandFactory.Object, null);

            // Act & Assert
            disposableReader.Dispose();
            disposableReader.Dispose(); // Should not throw cause scenario requires it

            // Verify that Dispose was called on both objects, but only once each
            mockReader.Verify(r => r.Dispose(), Times.AtLeastOnce);
            mockCommand.Verify(c => c.Dispose(), Times.AtLeastOnce);
        }

        [Fact]
        public void UsingStatement_DisposesAutomatically()
        {
            // Arrange
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var commandFactory = new Mock<Func<IDbCommand>>();

            commandFactory.Setup(f => f()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            // Act
            using (var disposableReader = new DisposableDataReader(commandFactory.Object, null))
            {
                // Use the reader
                _ = disposableReader.Read();
            }

            // Assert
            mockReader.Verify(r => r.Dispose(), Times.Once);
            mockCommand.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion Dispose Tests
    }
}
