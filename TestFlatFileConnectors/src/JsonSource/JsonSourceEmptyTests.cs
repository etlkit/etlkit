using EtlKit.DataFlow;

namespace EtlKit.TestFlatFileConnectors.JsonSource
{
    public class JsonSourceEmptyTests
    {
        [Fact]
        public void ReadEmptyArray()
        {
            EtlKit.DataFlow.JsonSource source = new EtlKit.DataFlow.JsonSource(
                "res/JsonSource/EmptyArray.json",
                ResourceType.File
            );
            var dest = new MemoryDestination();

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            Assert.True(dest.Data.Count == 0);
        }

        [Fact]
        public void JsonFromWebService()
        {
            EtlKit.DataFlow.JsonSource source = new EtlKit.DataFlow.JsonSource(
                "res/JsonSource/EmptyObject.json",
                ResourceType.File
            );
            var dest = new MemoryDestination();

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            Assert.True(dest.Data.Count == 0);
        }
    }
}
