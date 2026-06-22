using EtlKit.DataFlow;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestFlatFileConnectors.Helpers;

namespace EtlKit.TestFlatFileConnectors.JsonDestination
{
    [Collection("FlatFilesToDatabase")]
    public class JsonDestinationNullHandlingTests : FlatFileConnectorsTestBase
    {
        public JsonDestinationNullHandlingTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void IgnoreWithObject()
        {
            //Arrange
            var source = new MemorySource<MySimpleRow>
            {
                DataAsList = new List<MySimpleRow>
                {
                    null,
                    new() { Col1 = 1, Col2 = "Test1" },
                    null,
                    new() { Col1 = 2, Col2 = "Test2" },
                    new() { Col1 = 3, Col2 = "Test3" },
                    null,
                },
            };

            //Act
            var dest = new JsonDestination<MySimpleRow>(
                "./IgnoreNullValues.json",
                ResourceType.File
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./IgnoreNullValues.json"),
                File.ReadAllText("res/JsonDestination/TwoColumns.json").NormalizeLineEndings()
            );
        }

        [Fact]
        public void IgnoreWithStringArray()
        {
            //Arrange
            var source = new MemorySource<string[]>
            {
                DataAsList = new List<string[]>
                {
                    null,
                    new[] { "1", "Test1" },
                    null,
                    new[] { "2", "Test2" },
                    new[] { "3", "Test3" },
                    null,
                },
            };

            //Act
            var dest = new JsonDestination<string[]>(
                "./IgnoreNullValuesStringArray.json",
                ResourceType.File
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./IgnoreNullValuesStringArray.json"),
                File.ReadAllText("res/JsonDestination/TwoColumnsStringArray.json")
                    .NormalizeLineEndings()
            );
        }
    }
}
