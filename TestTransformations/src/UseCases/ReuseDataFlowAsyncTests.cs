using System.Threading;
using System.Threading.Tasks;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.UseCases
{
    public class ReuseDataFlowAsyncTests
    {
        [Fact]
        public async Task RunReusableFlow()
        {
            //Arrange
            var r1 = new ReferenceDataFlow(1, "Flow1");
            var r2 = new ReferenceDataFlow(1, "Flow2");

            //Act
            await Task.WhenAll(r1.Initialized, r2.Initialized);

            //Assert
            Assert.Equal("Flow1", r1.Data[1].Value);
            Assert.Equal("Flow2", r2.Data[1].Value);
        }

        public class MyData
        {
            public int Key { get; set; }
            public string Value { get; set; }
        }

        public class ReferenceDataFlow
        {
            private MemorySource<MyData> Source { get; set; }
            private CustomDestination<MyData> Destination { get; set; }

            public ReferenceDataFlow(int key, string value)
            {
                var x = new MyData { Key = key, Value = value };
                Source = new MemorySource<MyData>();
                Source.DataAsList.Add(x);
                Destination = new CustomDestination<MyData>(data => Data.Add(data.Key, data));
                Source.LinkTo(Destination);
                Source.ExecuteAsync(CancellationToken.None);
            }

            public IDictionary<long, MyData> Data { get; } = new Dictionary<long, MyData>();

            public Task Initialized => Destination.Completion;
        }
    }
}
