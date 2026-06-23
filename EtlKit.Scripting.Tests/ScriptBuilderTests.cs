using System.Dynamic;
using EtlKit.Scripting;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace EtlKit.Scripting.Tests;

public class ScriptBuilderTests
{
    [Fact]
    public async Task ShouldCompileScriptDirectly()
    {
        // Arrange
        var options = ScriptOptions.Default.WithImports("System.Math");
        var script = CSharpScript
            .Create("int X = 1; int Y = 2;", options: options)
            .ContinueWith("X + Y", options);
        // Act
        var diagnostics = script.Compile();
        var result = (await script.RunAsync()).ReturnValue;
        // Assert
        Assert.Empty(diagnostics);
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task ShouldEmitRuntimeErrorFromInvalidScript()
    {
        // Arrange
        var options = ScriptOptions.Default.WithImports("System.Math");
        var script = CSharpScript
            .Create("int X = W; int Y = 2;", options: options)
            .ContinueWith("X + Y", options);
        // Act
        script.Compile();
        var exception = await Assert.ThrowsAsync<CompilationErrorException>(
            async () => await script.RunAsync().ConfigureAwait(false)
        );
        // Assert
        Assert.NotNull(exception);
        Assert.Contains("W", exception.Message);
        Assert.Contains("error CS0103:", exception.Message); // The name 'W' does not exist in the current context
    }

    [Fact]
    public void ShouldCompileScriptWithDynamicGlobals()
    {
        // Arrange
        dynamic context = new ExpandoObject();
        context.X = 1;
        context.Y = 2;
        var builder = ScriptBuilder.Default.ForType(context);
        var runner = builder.CreateRunner("X + Y");
        // Act
        var diagnostics = runner.Script.Compile();
        var result = runner.RunAsync(context).Result.ReturnValue;
        // Assert
        Assert.Empty(diagnostics);
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task ShouldExecuteWithComplexNestedObject()
    {
        var factory = new ScriptBuilder();
        string script =
            "(Global1.Number+WhatEverNameIWant.Number+Global2.Number).ToString() + Global1.Text + WhatEverNameIWant.Text";

        dynamic globals = new ExpandoObject();
        globals.Global1 = new MyCoolClass { Number = 100, Text = "Something" };
        globals.WhatEverNameIWant = new MyCoolClass { Number = 500, Text = "Longer Text Value" };
        globals.Global2 = new ExpandoObject();
        globals.Global2.Number = 600;

        // Act
        var builder = factory.ForType(globals);
        var runner = builder.CreateRunner(script);
        var result = await runner.RunAsync(globals);

        // Assert
        Assert.Equal("1200SomethingLonger Text Value", result.ReturnValue);
    }

    [Fact]
    public void ShouldApplyValueToAllProperties()
    {
        // Arrange
        dynamic context = new ExpandoObject();
        context.X = "абра";
        context.Y = null;
        ScriptBuilder.Default.ForType(context);

        dynamic context2 = new ExpandoObject();
        context2.X = null;
        context2.Y = "кадабра";
        ScriptBuilder.Default.ForType(context2);

        dynamic context3 = new ExpandoObject();
        context3.X = "абра";
        context3.Y = "кадабра";
        var builder = ScriptBuilder.Default.ForType(context3);

        var runner = builder.CreateRunner(@"X + ""-"" + Y");
        // Act
        var diagnostics = runner.Script.Compile();
        var result = runner.RunAsync(context3).Result.ReturnValue;
        // Assert
        Assert.Empty(diagnostics);
        Assert.Equal("абра-кадабра", result);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class MyCoolClass
    {
        public string? Text { get; set; }
        public int Number { get; set; }
    }
}
