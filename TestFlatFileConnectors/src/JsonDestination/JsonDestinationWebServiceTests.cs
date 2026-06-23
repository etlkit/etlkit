using System.Net.Http;
using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using Moq;
using Moq.Contrib.HttpClient;

namespace EtlKit.TestFlatFileConnectors.JsonDestination
{
    [Collection("FlatFilesToDatabase")]
    public class JsonDestinationWebServiceTests : FlatFileConnectorsTestBase
    {
        public JsonDestinationWebServiceTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void WriteIntoHttpClient()
        {
            //Arrange
            //Arrange
            var handler = new Mock<HttpMessageHandler>();
            string result = null;

            handler
                .SetupAnyRequest()
                .Returns(
                    async (HttpRequestMessage request, CancellationToken _) =>
                    {
                        result = await request.Content!.ReadAsStringAsync(_).ConfigureAwait(false);
                        return new HttpResponseMessage
                        {
                            Content = new StringContent($"Hello, {result}"),
                        };
                    }
                )
                .Verifiable();

            var httpClient = handler.CreateClient();

            var source = new MemorySource<MySimpleRow>();
            var mySimpleRow = new MySimpleRow { Col1 = 1, Col2 = "Test1" };
            source.DataAsList.Add(mySimpleRow);

            //Act
            var dest = new JsonDestination<MySimpleRow>("http://test.test", ResourceType.Http)
            {
                HttpClient = httpClient,
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            handler.VerifyRequest(
                message =>
                    message.Method == HttpMethod.Post
                    && message.RequestUri == new Uri("http://test.test"),
                Times.Exactly(1)
            );
            Assert.Equal(
                JsonConvert.SerializeObject(new[] { mySimpleRow }, Formatting.Indented),
                result
            );
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MySimpleRow
        {
            public string Col2 { get; set; }
            public int Col1 { get; set; }
        }
    }
}
