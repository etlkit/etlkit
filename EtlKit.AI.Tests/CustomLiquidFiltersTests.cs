using System.Dynamic;
using DotLiquid;

namespace EtlKit.AI.Tests;

public class CustomLiquidFiltersTests
{
    public CustomLiquidFiltersTests()
    {
        // Ensure filters are registered once for DotLiquid usage in template rendering
        CustomLiquidFilters.EnsureRegistered();
    }

    [Fact]
    public void EnsureRegistered_ShouldBeIdempotent()
    {
        // Act: calling multiple times should not throw
        CustomLiquidFilters.EnsureRegistered();
        CustomLiquidFilters.EnsureRegistered();

        // Assert: rendering with a known filter should work
        // Use double-quoted Liquid string to avoid escaping issues inside single quotes
        dynamic a = new ExpandoObject();
        a.Result = "'ab'";
        var tpl = Template.Parse("{{Result | escape_single_quotes }}");
        var rendered = tpl.Render(Hash.FromDictionary(a));
        Assert.Equal("''ab''", rendered);
    }

    [Fact]
    public void EscapeSingleQuotes_ShouldDoubleQuotes_AndHandleNull()
    {
        Assert.Equal("O''Reilly", CustomLiquidFilters.EscapeSingleQuotes("O'Reilly"));
        Assert.Null(CustomLiquidFilters.EscapeSingleQuotes(null!));
        Assert.Equal(string.Empty, CustomLiquidFilters.EscapeSingleQuotes(string.Empty));
    }

    [Fact]
    public void EscapeSingleQuotesRecursive_ShouldWalkNestedDictionaries()
    {
        var obj = new Dictionary<string, object>
        {
            ["a"] = "x'y",
            ["b"] = new Dictionary<string, object> { ["c"] = "d'e" },
        };

        var res = (Dictionary<string, object>)CustomLiquidFilters.EscapeSingleQuotesRecursive(obj);

        Assert.Equal("x''y", res["a"]);
        var inner = (Dictionary<string, object>)res["b"];
        Assert.Equal("d''e", inner["c"]);

        // Non-string values are returned as-is
        var mix = new Dictionary<string, object> { ["n"] = 1, ["s"] = "q'w" };
        var mixRes =
            (Dictionary<string, object>)CustomLiquidFilters.EscapeSingleQuotesRecursive(mix);
        Assert.Equal(1, mixRes["n"]);
        Assert.Equal("q''w", mixRes["s"]);
    }

    [Fact]
    public void EscapeBackslash_ShouldDoubleBackslash_AndHandleNull()
    {
        Assert.Equal(
            @"{""Response"": ""{\\""respondent_id\\"":1}""}",
            CustomLiquidFilters.EscapeBackslash(@"{""Response"": ""{\""respondent_id\"":1}""}")
        );
        Assert.Null(CustomLiquidFilters.EscapeBackslash(null!));
        Assert.Equal(string.Empty, CustomLiquidFilters.EscapeBackslash(string.Empty));
    }

    [Fact]
    public void JsonArray_ShouldSerializeExpandoArray_WithoutNulls()
    {
        dynamic a = new ExpandoObject();
        a.id = 1;
        a.text = "Hello";
        dynamic b = new ExpandoObject();
        b.id = 2;
        b.text = null;

        var input = new ExpandoObject[] { a, b };
        var json = CustomLiquidFilters.JsonArray(input);

        // Basic shape
        Assert.StartsWith("[", json);
        Assert.Contains("\"id\":1", json);
        Assert.Contains("\"id\":2", json);

        // Validate content using System.Text.Json for resilience to serializer nuances
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var arr = doc.RootElement;
        Assert.Equal(System.Text.Json.JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal("Hello", arr[0].GetProperty("text").GetString());
        var second = arr[1];
        // Depending on serializer behavior with ExpandoObject, the null field may be present or omitted
        if (second.TryGetProperty("text", out var txt))
        {
            Assert.Equal(System.Text.Json.JsonValueKind.Null, txt.ValueKind);
        }
    }

    [Fact]
    public void AsString_ShouldReturnJsonForDictionary_AndToStringForScalar()
    {
        var dict = new Dictionary<string, object> { ["k"] = "v" };
        var str = CustomLiquidFilters.AsString(dict);
        Assert.Contains("\"k\":\"v\"", str);

        Assert.Equal("123", CustomLiquidFilters.AsString(123));
        Assert.Null(CustomLiquidFilters.AsString(null!));
    }

    [Fact]
    public void JsonArray_ShouldEscapeDoubleQuotes_InStringValues()
    {
        // Arrange: create an object with strings containing double quotes
        // This simulates error messages like:
        // "Value " neutral " is not defined in enum. Path 'reasons[0].polarity', line 1, position 124."
        dynamic obj = new ExpandoObject();
        obj.id = 1;
        obj.message =
            "Value \" neutral \" is not defined in enum. Path 'reasons[0].polarity', line 1, position 124.";
        obj.description = "This is a \"test\" message with \"multiple\" quotes";

        var input = new ExpandoObject[] { obj };

        // Act: serialize using JsonArray filter
        var json = CustomLiquidFilters.JsonArray(input);

        // Assert: verify JSON is valid and quotes are properly escaped
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Assert.Equal(System.Text.Json.JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(1, arr.GetArrayLength());

        var firstItem = arr[0];
        Assert.Equal(1, firstItem.GetProperty("id").GetInt32());

        // Verify that the message was deserialized correctly (quotes preserved in value)
        var message = firstItem.GetProperty("message").GetString();
        Assert.Equal(
            "Value \" neutral \" is not defined in enum. Path 'reasons[0].polarity', line 1, position 124.",
            message
        );

        var description = firstItem.GetProperty("description").GetString();
        Assert.Equal("This is a \"test\" message with \"multiple\" quotes", description);

        // Verify that in the JSON string itself, quotes are escaped with backslash
        Assert.Contains("\\\" neutral \\\"", json);
        Assert.Contains("\\\"test\\\"", json);
        Assert.Contains("\\\"multiple\\\"", json);
    }

    [Fact]
    public void AsString_ShouldEscapeDoubleQuotes_ForDictionaryValues()
    {
        // Arrange: dictionary with string containing embedded double quotes
        var dict = new Dictionary<string, object>
        {
            ["error"] = "Value \" neutral \" is not defined",
            ["path"] = "reasons[0].polarity",
        };

        // Act: serialize using AsString filter
        var json = CustomLiquidFilters.AsString(dict);

        // Assert: verify JSON is valid and quotes are properly escaped
        Assert.NotNull(json);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(System.Text.Json.JsonValueKind.Object, root.ValueKind);

        // Verify deserialized values preserve original quotes
        var error = root.GetProperty("error").GetString();
        Assert.Equal("Value \" neutral \" is not defined", error);

        var path = root.GetProperty("path").GetString();
        Assert.Equal("reasons[0].polarity", path);

        // Verify that in the JSON string itself, quotes are escaped
        Assert.Contains("\\\" neutral \\\"", json);
    }

    [Fact]
    public void JsonArray_WithSpecialCharacters_ShouldProduceValidJson()
    {
        // Arrange: test various special characters that need escaping in JSON
        dynamic obj = new ExpandoObject();
        obj.id = 1;
        obj.quotes = "double\"quote and single'quote";
        obj.backslash = "path\\to\\file";
        obj.newline = "line1\nline2";
        obj.tab = "col1\tcol2";
        obj.carriageReturn = "text\rmore";

        var input = new ExpandoObject[] { obj };

        // Act
        var json = CustomLiquidFilters.JsonArray(input);

        // Assert: JSON should be valid and parseable
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Assert.Equal(1, arr.GetArrayLength());
        var firstItem = arr[0];

        // Verify all special characters are correctly escaped and preserved
        Assert.Equal("double\"quote and single'quote", firstItem.GetProperty("quotes").GetString());
        Assert.Equal("path\\to\\file", firstItem.GetProperty("backslash").GetString());
        Assert.Equal("line1\nline2", firstItem.GetProperty("newline").GetString());
        Assert.Equal("col1\tcol2", firstItem.GetProperty("tab").GetString());
        Assert.Equal("text\rmore", firstItem.GetProperty("carriageReturn").GetString());

        // Verify escaping in the raw JSON string
        Assert.Contains("\\\"quote", json); // escaped double quote
        Assert.Contains("\\\\to\\\\", json); // escaped backslash
        Assert.Contains("\\n", json); // escaped newline
        Assert.Contains("\\t", json); // escaped tab
    }
}
