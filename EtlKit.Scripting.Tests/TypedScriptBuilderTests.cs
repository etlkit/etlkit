using System.Dynamic;
using EtlKit.Scripting;
using Microsoft.CodeAnalysis;

namespace EtlKit.Scripting.Tests;

public class TypedScriptBuilderTests
{
    [Fact]
    public void WithNullableContextOptions_DoesNotMutateOriginalBuilder()
    {
        // Arrange — original builder defaults to Disable.
        dynamic context = new ExpandoObject();
        context.X = "value";
        var original = ScriptBuilder.Default.ForType(context);

        // Act
        var modified = original.WithNullableContextOptions(NullableContextOptions.Enable);

        // Assert — separate instances; original keeps Disable behavior.
        Assert.NotSame(original, modified);
        Assert.NotEmpty(original.CreateRunner("(string?)X").Script.Compile());
        Assert.Empty(modified.CreateRunner("(string?)X").Script.Compile());
    }

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Warnings)]
    [InlineData(NullableContextOptions.Annotations)]
    [InlineData(NullableContextOptions.Enable)]
    public void WithNullableContextOptions_AllValues_CompileSimpleExpression(
        NullableContextOptions options
    )
    {
        // Arrange
        dynamic context = new ExpandoObject();
        context.X = 1;
        context.Y = 2;
        var builder = ScriptBuilder.Default.ForType(context).WithNullableContextOptions(options);

        // Act — a non-nullable-sensitive script must compile and run under any context value.
        var runner = builder.CreateRunner("X + Y");
        var diagnostics = runner.Script.Compile();
        var result = runner.RunAsync(context).Result.ReturnValue;

        // Assert
        Assert.Empty(diagnostics);
        Assert.Equal(3, result);
    }
}
