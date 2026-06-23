using System.Dynamic;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using Xunit;

namespace EtlKit.Json.Tests;

[Collection("Transformations")]
public class JsonTransformationTests
{
    [Fact]
    public async Task DestinationJsonTransformationTest()
    {
        //Arrange
        ExpandoObject[] objSet =
        [
            CreateObject(@"{ ""Data"": { ""Id"": 1, ""Name"": ""Test1"" } }"),
            CreateObject(@"{ ""Data"": { ""Id"": 2, ""Name"": ""Test2"" } }"),
            CreateObject(@"{ ""Data"": { ""Id"": 3, ""Name"": ""Test3"" } }"),
        ];

        var source = new MemorySource<ExpandoObject>(objSet);

        //Act
        var trans = new JsonTransformation
        {
            Mappings =
            {
                ["Col1"] = new JsonTransformation.Mapping("data", "$.Data.Id"),
                ["Col2"] = new JsonTransformation.Mapping("data", "$.Data.Name"),
            },
        };

        var dest = new MemoryDestination<ExpandoObject>();
        source.LinkTo(trans);
        trans.LinkTo(dest);
        await source.ExecuteAsync(CancellationToken.None);
        await dest.Completion.ConfigureAwait(true);

        //Assert
        Assert.Equal(3, dest.Data.Count);
        Assert.Collection(
            dest.Data,
            (dynamic d) =>
            {
                Assert.Equal(1, d.Col1);
                Assert.Equal("Test1", d.Col2);
            },
            d =>
            {
                Assert.Equal(2, d.Col1);
                Assert.Equal("Test2", d.Col2);
            },
            d =>
            {
                Assert.Equal(3, d.Col1);
                Assert.Equal("Test3", d.Col2);
            }
        );
    }

    [Fact]
    public async Task JsonTransformation_ShouldGetAnObjectFromSourceField()
    {
        //Arrange
        dynamic obj = new ExpandoObject();
        obj.EventId = Guid.NewGuid();

        var source = new MemorySource<ExpandoObject>([(ExpandoObject)obj]);

        //Act
        var trans = new JsonTransformation
        {
            Mappings = { ["Col1"] = new JsonTransformation.Mapping("EventId", null!) },
        };

        var dest = new MemoryDestination<ExpandoObject>();
        source.LinkTo(trans);
        trans.LinkTo(dest);
        await source.ExecuteAsync(CancellationToken.None);
        await dest.Completion.ConfigureAwait(true);

        //Assert
        Assert.Single(dest.Data);
        var res = dest.Data.First() as IDictionary<string, object?>;
        Assert.Equal(obj.EventId, res["Col1"]);
    }

    [Fact]
    public void ParseNative_ShouldReturnNativeTypedExpandoObject()
    {
        const string json =
            """{"Name":"Alice","Score":100,"Active":true,"Date":"2024-01-15T10:00:00","Ratio":1.5}""";

        var result = (IDictionary<string, object?>)JsonTransformation.ParseNative(json);

        Assert.Equal("Alice", result["Name"]);
        Assert.Equal(100, result["Score"]);
        Assert.True((bool)result["Active"]!);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Unspecified), result["Date"]);
        Assert.Equal(1.5, result["Ratio"]);
    }

    [Fact]
    public void ParseNative_ShouldHandleNestedObjects()
    {
        const string json = """{"Outer":{"Inner":"value"}}""";

        var result = (IDictionary<string, object?>)JsonTransformation.ParseNative(json);

        var nested = Assert.IsType<ExpandoObject>(result["Outer"]);
        Assert.Equal("value", ((IDictionary<string, object?>)nested)["Inner"]);
    }

    [Fact]
    public async Task JsonTransformation_NonExistentPath_ReturnsNull()
    {
        //Arrange
        ExpandoObject[] objSet = [CreateObject("""{ "Data": { "Id": 1 } }""")];

        var source = new MemorySource<ExpandoObject>(objSet);

        //Act
        var trans = new JsonTransformation
        {
            Mappings = { ["Col1"] = new JsonTransformation.Mapping("data", "$.Data.NonExistent") },
        };

        var dest = new MemoryDestination<ExpandoObject>();
        source.LinkTo(trans);
        trans.LinkTo(dest);
        await source.ExecuteAsync(CancellationToken.None);
        await dest.Completion.ConfigureAwait(true);

        //Assert
        Assert.Single(dest.Data);
        var res = dest.Data.First() as IDictionary<string, object?>;
        Assert.Null(res!["Col1"]);
    }

    private static ExpandoObject CreateObject(string v)
    {
        dynamic obj = new ExpandoObject();
        obj.data = v;
        return (ExpandoObject)obj;
    }
}
