using System.Threading;
using EtlKit.Common;
using EtlKit.DataFlow;

namespace EtlKit.TestConnectionManager.ConnectionManager
{
    public class NoConnectionManagerTests
    {
        [Fact]
        public void DbSource()
        {
            //Arrange
            DbSource<string[]> source = new DbSource<string[]>("test");
            MemoryDestination<string[]> dest = new MemoryDestination<string[]>();
            source.LinkTo(dest);

            //Act & Assert
            Assert.Throws<EtlKitException>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }

        [Fact]
        public void DbDestination()
        {
            //Arrange
            string[] data = { "1", "2" };
            MemorySource<string[]> source = new MemorySource<string[]>();
            source.DataAsList.Add(data);
            DbDestination<string[]> dest = new DbDestination<string[]>("test");
            source.LinkTo(dest);

            //Act & Assert
            Assert.Throws<EtlKitException>(() =>
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
    }
}
