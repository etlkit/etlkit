using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Helper;
using EtlKit.TestFlatFileConnectors.Fixture;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestFlatFileConnectors.JsonSource
{
    [Collection("FlatFilesToDatabase")]
    public class JsonSourceConverterTests : FlatFileConnectorsTestBase
    {
        public JsonSourceConverterTests(FlatFileToDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void JsonPathInJsonPropertyAttribute()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("JsonSourceNested");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "JsonSourceNested");

            //Act
            var source = new JsonSource<MySimpleRow>(
                "res/JsonSource/NestedData.json",
                ResourceType.File
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Fact]
        public void JsonPathInExpandoObject()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture("JsonSourceNestedDynamic");
            var trans = new RowTransformation<ExpandoObject>(row =>
            {
                dynamic r = row;
                r.Col1 = r.Column1;
                return r;
            });
            var dest = new DbDestination<ExpandoObject>(SqlConnection, "JsonSourceNestedDynamic");

            //Act
            var source = new JsonSource<ExpandoObject>(
                "res/JsonSource/NestedData.json",
                ResourceType.File
            );
            var pathLookups = new List<JsonProperty2JsonPath>
            {
                new()
                {
                    JsonPropertyName = "Column2",
                    JsonPath = "Value",
                    NewPropertyName = "Col2",
                },
            };
            source.JsonSerializer.Converters.Add(new ExpandoJsonPathConverter(pathLookups));

            source.LinkTo(trans).LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Fact]
        public void JsonPathListIntoDynamic()
        {
            var saveCulture = CultureInfo.CurrentCulture;
            try
            {
                //Arrange
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                var dest4Columns = new FourColumnsTableFixture("JsonSourceNestedDynamic4Cols");
                var dest = new DbDestination<ExpandoObject>(
                    SqlConnection,
                    "JsonSourceNestedDynamic4Cols"
                );

                //Act
                var source = new JsonSource<ExpandoObject>(
                    "res/JsonSource/NestedData4Cols.json",
                    ResourceType.File
                );
                var pathLookups = new List<JsonProperty2JsonPath>
                {
                    new() { JsonPropertyName = "Col2", JsonPath = "Value" },
                    new()
                    {
                        JsonPropertyName = "Object",
                        JsonPath = "Number[0]",
                        NewPropertyName = "Col4",
                    },
                    new()
                    {
                        JsonPropertyName = "Array",
                        JsonPath = "[1].Value",
                        NewPropertyName = "Col3",
                    },
                };
                source.JsonSerializer.Converters.Add(new ExpandoJsonPathConverter(pathLookups));

                source.LinkTo(dest);
                source.Execute();
                dest.Wait();

                //Assert
                dest4Columns.AssertTestData();
                Assert.Equal(
                    1,
                    RowCountTask.Count(
                        SqlConnection,
                        "JsonSourceNestedDynamic4Cols",
                        "Col2 = 'Test2' AND Col3 = 4711 AND Col4='1.23'"
                    )
                );
            }
            finally
            {
                CultureInfo.CurrentCulture = saveCulture;
            }
        }

        [JsonConverter(typeof(JsonPathConverter))]
        public class MySimpleRow
        {
            [JsonProperty("Column1")]
            public int Col1 { get; set; }

            [JsonProperty("Column2.Value")]
            public string Col2 { get; set; }
        }
    }
}
