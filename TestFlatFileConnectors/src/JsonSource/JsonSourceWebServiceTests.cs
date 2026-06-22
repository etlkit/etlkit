using System.Net;
using System.Net.Http;
using EtlKit.DataFlow;
using Moq;
using Moq.Protected;

namespace EtlKit.TestFlatFileConnectors.JsonSource
{
    public class JsonSourceWebServiceTests
    {
        public class Todo
        {
            [JsonProperty("Id")]
            public int Key { get; set; }
            public string Title { get; set; }
        }

        [Fact]
        public void JsonFromWebService()
        {
            // Arrange
            var response = new HttpResponseMessage();
            var httpClient = MoqJsonResponse(
                File.ReadAllText("res/JsonSource/Todos.json"),
                response
            );

            //Arrange
            var dest = new MemoryDestination<Todo>();

            //Act
            var source = new JsonSource<Todo>("https://test.com/") { HttpClient = httpClient };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.All(dest.Data, item => Assert.True(item.Key > 0));
            Assert.Equal(5, dest.Data.Count);

            httpClient.Dispose();
            response.Dispose();
        }

        private static HttpClient MoqJsonResponse(string json, HttpResponseMessage response)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StringContent(json);

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response)
                .Verifiable();

            return new HttpClient(handlerMock.Object);
        }

        [Fact]
        public void PaginatedRequest()
        {
            //Arrange
            var dest = new MemoryDestination<Todo>();
            var page = 1;
            //Act
            var source = new JsonSource<Todo>
            {
                GetNextUri = _ => "res/JsonSource/Todos_Page" + page++ + ".json",
                HasNextUri = _ => page <= 3,
                ResourceType = ResourceType.File,
            };

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.All(dest.Data, item => Assert.True(item.Key > 0));
            Assert.Equal(5, dest.Data.Count);
        }
    }
}
