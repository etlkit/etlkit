using System.Dynamic;
using System.Text.Json;

namespace EtlKit.AI.Tests;

public class ExpandoObjectConverterTests
{
    private static JsonSerializerOptions CreateOptionsWithPublicConverter()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ExpandoObjectConverter());
        return options;
    }

    [Fact]
    public void Read_ShouldHandlePrimitivesNestedObjectsAndArrays()
    {
        // Arrange
        var json =
            "{"
            + "\"s\":\"text\","
            + "\"i\":1,"
            + "\"d\":1.5,"
            + "\"b1\":true,"
            + "\"b0\":false,"
            + "\"n\":null,"
            + "\"obj\":{\"a\":2},"
            + "\"arr\":[1,\"x\",null,{\"k\":2}]"
            + "}";

        var options = CreateOptionsWithPublicConverter();

        // Act
        var exp = JsonSerializer.Deserialize<ExpandoObject>(json, options)!;

        // Assert
        var d = (IDictionary<string, object>)exp;
        Assert.Equal("text", d["s"]);
        Assert.Equal(1L, d["i"]);
        Assert.Equal(1.5, d["d"]);
        Assert.True((bool)d["b1"]);
        Assert.False((bool)d["b0"]);
        Assert.Contains("n", d.Keys);
        Assert.Null(d["n"]);

        Assert.IsAssignableFrom<ExpandoObject>(d["obj"]);
        var obj = (IDictionary<string, object>)(ExpandoObject)d["obj"]!;
        Assert.Equal(2L, obj["a"]);

        Assert.IsAssignableFrom<object[]>(d["arr"]);
        var arr = (object[])d["arr"]!;
        Assert.Equal(4, arr.Length);
        Assert.Equal(1L, arr[0]);
        Assert.Equal("x", arr[1]);
        Assert.Null(arr[2]);
        Assert.IsAssignableFrom<ExpandoObject>(arr[3]);
        var arrObj = (IDictionary<string, object>)(ExpandoObject)arr[3]!;
        Assert.Equal(2L, arrObj["k"]);
    }

    [Fact]
    public void Read_ShouldThrow_OnNonObjectRoot()
    {
        // Arrange: the root is an array while the converter expects an object at the root
        var json = "[1,2,3]";
        var options = CreateOptionsWithPublicConverter();

        // Act
        Action act = () => JsonSerializer.Deserialize<ExpandoObject>(json, options);

        // Assert
        Assert.Throws<JsonException>(act);
    }

    [Fact]
    public void Roundtrip_SerializeWithDefaultDeserializeWithConverter_ShouldPreserveShape()
    {
        // Arrange: serialize ExpandoObject using the default serializer, then deserialize with the converter
        dynamic src = new ExpandoObject();
        src.name = "alice";
        src.age = 33;
        src.flags = new object[] { true, null, 1.2 };
        src.addr = new ExpandoObject();
        src.addr.city = "NY";

        var json = JsonSerializer.Serialize((object)src); // default serialization (without the converter)
        var options = CreateOptionsWithPublicConverter();

        // Act
        var dst = JsonSerializer.Deserialize<ExpandoObject>(json, options)!;

        // Assert (partial shape verification)
        var d = (IDictionary<string, object>)dst;
        Assert.Equal("alice", d["name"]);
        Assert.Equal(33L, d["age"]);
        Assert.Contains("city", ((IDictionary<string, object>)d["addr"]).Keys);
        var flags = (object[])d["flags"]!;
        Assert.True((bool)flags[0]);
        Assert.Null(flags[1]);
    }
}
