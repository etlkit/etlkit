using System.Text.Json.Nodes;
using EtlKit.ControlFlow;
using EtlKit.TestOtherConnectors.Fixture;

namespace EtlKit.TestOtherConnectors.CustomSource
{
    [Collection("OtherConnectors")]
    public class CustomSourceJsonTests : OtherConnectorsTestBase
    {
        public CustomSourceJsonTests(OtherConnectorsDatabaseFixture fixture)
            : base(fixture) { }

        /// <summary>
        /// See https://jsonplaceholder.typicode.com/ for details of the rest api
        /// used for this test
        /// </summary>
        [Fact]
        public void CustomSourceWithWebService()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Create test table",
                @"CREATE TABLE dbo.WebServiceDestination 
                ( Id INT NOT NULL, UserId INT NOT NULL, Title NVARCHAR(100) NOT NULL, Completed BIT NOT NULL )"
            );
            DbDestination<Todo> dest = new DbDestination<Todo>(
                SqlConnection,
                "dbo.WebServiceDestination"
            );
            WebserviceFakeReader wsreader = new WebserviceFakeReader();

            //Act
            CustomSource<Todo> source = new CustomSource<Todo>(
                wsreader.ReadTodo,
                wsreader.EndOfData
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(5, RowCountTask.Count(SqlConnection, "dbo.WebServiceDestination"));
        }

        [Serializable]
        public class Todo
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string Title { get; set; }
            public bool Completed { get; set; }
        }

        [Serializable]
        public class WebserviceFakeReader
        {
            private readonly JsonArray _todosJsonArray;

            public WebserviceFakeReader()
            {
                var fileContent = File.ReadAllText(
                    Path.Join(
                        Directory.GetCurrentDirectory(),
                        "res",
                        "CustomSource",
                        "typicode.com.todos.json"
                    )
                );
                _todosJsonArray = JsonNode.Parse(fileContent) as JsonArray;
            }

            public string Json { get; set; }
            public int TodoCounter { get; set; } = 1;

            public Todo ReadTodo()
            {
                var todo = new Todo();
                TodoCounter++;
                var response = _todosJsonArray[TodoCounter]!.ToString();
                JsonConvert.PopulateObject(response, todo);
                return todo;
            }

            public bool EndOfData()
            {
                return TodoCounter > 5;
            }
        }
    }
}
