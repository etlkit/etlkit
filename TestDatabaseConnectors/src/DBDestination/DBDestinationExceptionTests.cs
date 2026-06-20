using System.Threading;
using EtlKit;
using EtlKit.DataFlow;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationExceptionTests : DatabaseConnectorsTestBase
    {
        public DbDestinationExceptionTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        [Fact]
        public void UnknownTable()
        {
            //Arrange
            string[] data = { "1", "2" };
            MemorySource<string[]> source = new MemorySource<string[]>();
            source.DataAsList.Add(data);
            DbDestination<string[]> dest = new DbDestination<string[]>(
                SqlConnection,
                "UnknownTable"
            );
            source.LinkTo(dest);

            //Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                try
                {
                    source.Execute(CancellationToken.None);
                    dest.Wait();
                }
                catch (AggregateException e)
                {
                    throw e.InnerException!;
                }
            });
        }

        [Fact]
        public void UnknownTableViaTableDefinition()
        {
            //Arrange
            TableDefinition def = new TableDefinition(
                "UnknownTable",
                new List<TableColumn> { new("id", "INT") }
            );

            //Arrange
            string[] data = { "1", "2" };
            MemorySource<string[]> source = new MemorySource<string[]>();
            source.DataAsList.Add(data);
            DbDestination<string[]> dest = new DbDestination<string[]>
            {
                ConnectionManager = SqlConnection,
                DestinationTableDefinition = def,
            };
            source.LinkTo(dest);

            //Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                try
                {
                    source.Execute();
                    dest.Wait();
                }
                catch (AggregateException e)
                {
                    throw e.InnerException!;
                }
            });
        }
    }
}
