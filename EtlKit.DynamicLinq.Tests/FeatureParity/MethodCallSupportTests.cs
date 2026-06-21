using System.Dynamic;
using System.Linq.Dynamic.Core;
using EtlKit.DataFlow;
using EtlKit.DynamicLinq;
using EtlKit.Primitives;
using EtlKit.Scripting;

namespace EtlKit.DynamicLinq.Tests.FeatureParity;

/// <summary>
/// Feature-parity tests for the two expression engines used by EtlKit.Scripting:
/// Roslyn (via <see cref="ScriptBuilder"/>) and System.Linq.Dynamic.Core (via
/// <c>AsQueryable().Where/Any</c>).
///
/// These are not benchmarks - they document Yes/No on specific feature categories
/// raised by the reviewer (note 84243): "Вызов методов кажется супер важным,
/// без него мы не сможем работать со сложными типами, элементарно JsonNode в
/// строку не сможем преобразовать."
///
/// The matrix produced by these tests feeds the report and the
/// TECH-DEBT-Expression-Engine-Unification.md analysis section.
/// </summary>
public class MethodCallSupportTests
{
    private static readonly ParsingConfig s_parsingConfig =
        new() { ConvertObjectToSupportComparison = true };

    // ---- Built-in instance method on a string property ---------------------

    [Fact]
    public async Task Roslyn_StringLength_Works()
    {
        var row = MakeRow(("Type", (object)"Day"));
        var dict = (IDictionary<string, object?>)row;

        var runner = ScriptBuilder.Default.ForType(dict).CreateRunner<bool>("Type.Length > 0");

        var result = (await runner.RunAsync(dict)).ReturnValue;
        Assert.True(result);
    }

    [Fact]
    public void DynamicLinq_StringLength_Works()
    {
        var row = MakeRow(("Type", (object)"Day"));
        var (type, instance) = ExpandoTypeMapper.Map(row);
        var array = Array.CreateInstance(type, 1);
        array.SetValue(instance, 0);

        var passed = array.AsQueryable().Any(s_parsingConfig, "Type.Length > 0");
        Assert.True(passed);
    }

    // ---- Built-in instance method on DateTime ------------------------------

    [Fact]
    public async Task Roslyn_DateTimeAddDays_Works()
    {
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var row = MakeRow(("Date", (object)date));
        var dict = (IDictionary<string, object?>)row;

        var runner = ScriptBuilder
            .Default.ForType(dict)
            .CreateRunner<bool>("Date.AddDays(1).Year == 2026");

        var result = (await runner.RunAsync(dict)).ReturnValue;
        Assert.True(result);
    }

    [Fact]
    public void DynamicLinq_DateTimeAddDays_Works()
    {
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var row = MakeRow(("Date", (object)date));
        var (type, instance) = ExpandoTypeMapper.Map(row);
        var array = Array.CreateInstance(type, 1);
        array.SetValue(instance, 0);

        var passed = array.AsQueryable().Any(s_parsingConfig, "Date.AddDays(1).Year == 2026");
        Assert.True(passed);
    }

    // ---- Method call on user type (the JsonNode-style case in note 84243) --

    [Fact]
    public async Task Roslyn_UserTypeMethod_Works()
    {
        // SimpleBox is a tiny stand-in for any user type with a ToText() method.
        // Roslyn just compiles and dispatches - works out of the box.
        var row = MakeRow(("Box", (object)new SimpleBox(42)));
        var dict = (IDictionary<string, object?>)row;

        var runner = ScriptBuilder
            .Default.ForType(dict)
            .CreateRunner<bool>("Box.ToText() == \"box(42)\"");

        var result = (await runner.RunAsync(dict)).ReturnValue;
        Assert.True(result);
    }

    [Fact]
    public void DynamicLinq_UserTypeMethod_DoesNotWork_WithoutCustomTypes()
    {
        // Without registered custom types, calling a user type method throws.
        // "User type" here = type defined outside System / framework BCL; Dynamic LINQ
        // recognizes BCL types out of the box (string, DateTime, int, byte[]), but not
        // user-defined classes. SimpleBox stands in for any such type.
        var filtration = new ExpressionRowFiltration<RowWithBox>("Box.ToText() == \"box(42)\"");
        var rows = new[] { new RowWithBox { Box = new SimpleBox(42) } };
        var source = new MemorySource<RowWithBox>();
        foreach (var row in rows)
            source.DataAsList.Add(row);
        var dest = new MemoryDestination<RowWithBox>();
        var errorDest = new MemoryDestination<EtlKitError>();
        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        filtration.LinkErrorTo(errorDest);
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        // Without RegisterCustomTypes, the parse fails per row, error goes to error buffer.
        Assert.Empty(dest.Data);
        Assert.Single(errorDest.Data);
    }

    [Fact]
    public void DynamicLinq_UserTypeMethod_Works_AfterRegisterCustomTypes()
    {
        // After RegisterCustomTypes(typeof(SimpleBox)), the user type is visible to
        // Dynamic LINQ's parser and its instance methods become callable from the
        // expression text. This closes the feature gap raised in note 84400.
        var filtration = new ExpressionRowFiltration<RowWithBox>("Box.ToText() == \"box(42)\"");
        filtration.RegisterCustomTypes(typeof(SimpleBox));
        var rows = new[]
        {
            new RowWithBox { Box = new SimpleBox(42) }, // passes
            new RowWithBox { Box = new SimpleBox(99) }, // dropped
        };
        var source = new MemorySource<RowWithBox>();
        foreach (var row in rows)
            source.DataAsList.Add(row);
        var dest = new MemoryDestination<RowWithBox>();
        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        source.Execute();
        dest.Wait();

        Assert.Single(dest.Data);
        Assert.Equal(42, dest.Data.First().Box!.Value);
    }

    private sealed class RowWithBox
    {
        public SimpleBox? Box { get; set; }
    }

    // ---- Phase 1: AdditionalAssemblyNames bulk registration ---------------

    [Fact]
    public void DynamicLinq_AdditionalAssemblyNames_RegistersAllPublicTypes()
    {
        // Without per-type RegisterCustomTypes, listing the assembly that declares
        // SimpleBox in AdditionalAssemblyNames should make all its public types
        // resolvable. This is the bulk-registration path symmetric with
        // ScriptedRowTransformation.AdditionalAssemblyNames in MR !113.
        var filtration = new ExpressionRowFiltration<RowWithBox>("Box.ToText() == \"box(7)\"");
        filtration.AdditionalAssemblyNames = new[] { typeof(SimpleBox).Assembly.GetName().Name! };

        var rows = new[]
        {
            new RowWithBox { Box = new SimpleBox(7) }, // passes
            new RowWithBox { Box = new SimpleBox(99) }, // dropped
        };
        var source = new MemorySource<RowWithBox>();
        foreach (var row in rows)
            source.DataAsList.Add(row);
        var dest = new MemoryDestination<RowWithBox>();
        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        source.Execute();
        dest.Wait();

        Assert.Single(dest.Data);
        Assert.Equal(7, dest.Data.First().Box!.Value);
    }

    [Fact]
    public void DynamicLinq_AdditionalAssemblyNames_GetterRoundTrip()
    {
        // Round-trip through the property getter so XML deserialization scenarios
        // (which read back via reflection) see the value they set.
        var filtration = new ExpressionRowFiltration<RowWithBox>();
        filtration.AdditionalAssemblyNames = new[] { typeof(SimpleBox).Assembly.GetName().Name! };

        Assert.Contains(
            typeof(SimpleBox).Assembly.GetName().Name,
            filtration.AdditionalAssemblyNames
        );
    }

    [Fact]
    public void DynamicLinq_AdditionalAssemblyNames_BadName_Throws()
    {
        var filtration = new ExpressionRowFiltration<RowWithBox>();

        var ex = Assert.Throws<InvalidOperationException>(
            () => filtration.AdditionalAssemblyNames = new[] { "Definitely.Not.A.Real.Assembly" }
        );
        Assert.Contains("Could not load assembly", ex.Message);
    }

    // ---- Phase 1: AdditionalImports namespace shortcuts -------------------

    [Fact]
    public void DynamicLinq_AdditionalImports_ResolvesShortNameInNamespace()
    {
        // With AdditionalImports = ["EtlKit.DynamicLinq.Tests.FeatureParity"], the
        // parser can resolve a short type name "SimpleBox" against that namespace
        // even though it would otherwise need the fully qualified name.
        var filtration = new ExpressionRowFiltration<RowWithBox>("Box.ToText() == \"box(13)\"");
        filtration.AdditionalAssemblyNames = new[] { typeof(SimpleBox).Assembly.GetName().Name! };
        filtration.AdditionalImports = new[] { typeof(SimpleBox).Namespace! };

        var rows = new[]
        {
            new RowWithBox { Box = new SimpleBox(13) }, // passes
            new RowWithBox { Box = new SimpleBox(99) }, // dropped
        };
        var source = new MemorySource<RowWithBox>();
        foreach (var row in rows)
            source.DataAsList.Add(row);
        var dest = new MemoryDestination<RowWithBox>();
        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        source.Execute();
        dest.Wait();

        Assert.Single(dest.Data);
    }

    [Fact]
    public void DynamicLinq_AdditionalImports_GetterRoundTrip()
    {
        var filtration = new ExpressionRowFiltration<RowWithBox>();
        filtration.AdditionalImports = new[] { "MyCompany.Domain", "Other.Namespace" };

        var actual = filtration.AdditionalImports.ToArray();
        Assert.Equal(new[] { "MyCompany.Domain", "Other.Namespace" }, actual);
    }

    [Fact]
    public void DynamicLinq_AdditionalAssemblyNames_GetterReturnsExactInputStrings()
    {
        // Round-trip: whatever the caller wrote as input - short name, full
        // assembly-qualified name, or file path - the getter should return
        // exactly that string. The internal Assembly[] is a load-side detail.
        var filtration = new ExpressionRowFiltration<RowWithBox>();
        var fullName = typeof(SimpleBox).Assembly.GetName().FullName!;

        filtration.AdditionalAssemblyNames = new[] { fullName };

        Assert.Equal(new[] { fullName }, filtration.AdditionalAssemblyNames);
    }

    // Note: short-name ambiguity detection is unit-tested directly on the provider
    // in DynamicLinqTypeProviderTests (ResolveTypeBySimpleName_AmbiguousWithoutImports_Throws
    // and ResolveTypeBySimpleName_ResolvedByImports_ReturnsImportedType). Same scenarios
    // through the filtration wrapper are covered via the typed expression path here.

    // ---- Static method call ------------------------------------------------

    [Fact]
    public async Task Roslyn_StaticFormat_Works()
    {
        var row = MakeRow(("Type", (object)"Day"));
        var dict = (IDictionary<string, object?>)row;

        var runner = ScriptBuilder
            .Default.ForType(dict)
            .CreateRunner<bool>("string.Format(\"{0}\", Type) == \"Day\"");

        var result = (await runner.RunAsync(dict)).ReturnValue;
        Assert.True(result);
    }

    [Fact]
    public void DynamicLinq_StaticFormat_Works()
    {
        // Dynamic LINQ supports static methods on built-in framework types out of
        // the box: string.Format, Math.Max, DateTime.Parse, etc. The capability gap
        // (see DynamicLinq_UserTypeMethod_DoesNotWork_OutOfBox above) is specifically
        // for *user* types - those need ParsingConfig.CustomTypeProvider registration.
        var row = MakeRow(("Type", (object)"Day"));
        var (type, instance) = ExpandoTypeMapper.Map(row);
        var array = Array.CreateInstance(type, 1);
        array.SetValue(instance, 0);

        var passed = array
            .AsQueryable()
            .Any(s_parsingConfig, "string.Format(\"{0}\", Type) == \"Day\"");
        Assert.True(passed);
    }

    // ---- Helpers -----------------------------------------------------------

    private static ExpandoObject MakeRow(params (string key, object value)[] fields)
    {
        var row = new ExpandoObject();
        var dict = (IDictionary<string, object?>)row;
        foreach (var (key, value) in fields)
            dict[key] = value;
        return row;
    }

    public sealed class SimpleBox
    {
        public int Value { get; }

        public SimpleBox(int value) => Value = value;

        public string ToText() => $"box({Value})";
    }
}
