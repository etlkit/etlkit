using EtlKit.DynamicLinq;

namespace EtlKit.DynamicLinq.Tests;

// Direct unit tests for DynamicLinqTypeProvider. Earlier coverage came only via
// ExpressionRowFiltration (note 84543) - here we exercise each public method on
// the provider directly so the resolution rules are documented and pinned.
public class DynamicLinqTypeProviderTests
{
    [Fact]
    public void GetCustomTypes_ReturnsRegisteredTypes()
    {
        var provider = new DynamicLinqTypeProvider(
            new HashSet<Type> { typeof(string), typeof(DateTime) }
        );

        var custom = provider.GetCustomTypes();

        Assert.Contains(typeof(string), custom);
        Assert.Contains(typeof(DateTime), custom);
    }

    [Fact]
    public void GetExtensionMethods_ReturnsEmptyMap()
    {
        var provider = new DynamicLinqTypeProvider(new HashSet<Type>());

        var ext = provider.GetExtensionMethods();

        Assert.Empty(ext);
    }

    [Fact]
    public void ResolveType_FullName_DirectMatch()
    {
        var provider = new DynamicLinqTypeProvider(new HashSet<Type> { typeof(DateTime) });

        var resolved = provider.ResolveType("System.DateTime");

        Assert.Equal(typeof(DateTime), resolved);
    }

    [Fact]
    public void ResolveType_ShortName_DirectMatch()
    {
        var provider = new DynamicLinqTypeProvider(new HashSet<Type> { typeof(DateTime) });

        var resolved = provider.ResolveType("DateTime");

        Assert.Equal(typeof(DateTime), resolved);
    }

    [Fact]
    public void ResolveType_NotRegistered_ReturnsNull()
    {
        var provider = new DynamicLinqTypeProvider(new HashSet<Type> { typeof(string) });

        var resolved = provider.ResolveType("System.DateTime");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveType_AmbiguousShortName_Throws()
    {
        // Same ambiguity contract as ResolveTypeBySimpleName: two registered
        // types share a short name, no import disambiguates - throw with a
        // pointer to AdditionalImports rather than silently picking one.
        var provider = new DynamicLinqTypeProvider(
            new HashSet<Type> { typeof(System.Threading.Timer), typeof(System.Timers.Timer) }
        );

        var ex = Assert.Throws<InvalidOperationException>(() => provider.ResolveType("Timer"));
        Assert.Contains("Ambiguous short type name 'Timer'", ex.Message);
        Assert.Contains("AdditionalImports", ex.Message);
    }

    [Fact]
    public void ResolveType_ShortNameInImports_ReturnsImportedType()
    {
        var provider = new DynamicLinqTypeProvider(
            new HashSet<Type> { typeof(System.Threading.Timer) },
            new[] { "System.Threading" }
        );

        var resolved = provider.ResolveType("Timer");

        Assert.Equal(typeof(System.Threading.Timer), resolved);
    }

    [Fact]
    public void ResolveTypeBySimpleName_ResolvedByImports_ReturnsImportedType()
    {
        var provider = new DynamicLinqTypeProvider(
            new HashSet<Type> { typeof(System.Threading.Timer), typeof(System.Timers.Timer) },
            new[] { "System.Timers" }
        );

        var resolved = provider.ResolveTypeBySimpleName("Timer");

        Assert.Equal(typeof(System.Timers.Timer), resolved);
    }

    [Fact]
    public void ResolveTypeBySimpleName_AmbiguousWithoutImports_Throws()
    {
        var provider = new DynamicLinqTypeProvider(
            new HashSet<Type> { typeof(System.Threading.Timer), typeof(System.Timers.Timer) }
        );

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.ResolveTypeBySimpleName("Timer")
        );
        Assert.Contains("Ambiguous short type name 'Timer'", ex.Message);
        Assert.Contains("AdditionalImports", ex.Message);
    }

    [Fact]
    public void ResolveTypeBySimpleName_SingleMatch_ReturnsMatch()
    {
        var provider = new DynamicLinqTypeProvider(new HashSet<Type> { typeof(DateTime) });

        var resolved = provider.ResolveTypeBySimpleName("DateTime");

        Assert.Equal(typeof(DateTime), resolved);
    }

    [Fact]
    public void ResolveTypeBySimpleName_NotRegistered_ReturnsNull()
    {
        var provider = new DynamicLinqTypeProvider(new HashSet<Type> { typeof(string) });

        var resolved = provider.ResolveTypeBySimpleName("DateTime");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveTypeBySimpleName_ImportsAreCheckedInOrder()
    {
        // Both imports could resolve - first one wins. Documents declaration-order
        // semantics so consumers can rely on it for shadowing.
        var provider = new DynamicLinqTypeProvider(
            new HashSet<Type> { typeof(System.Threading.Timer), typeof(System.Timers.Timer) },
            new[] { "System.Threading", "System.Timers" }
        );

        var resolved = provider.ResolveTypeBySimpleName("Timer");

        Assert.Equal(typeof(System.Threading.Timer), resolved);
    }

    [Fact]
    public void Constructor_NullImports_TreatedAsEmpty()
    {
        var provider = new DynamicLinqTypeProvider(
            new HashSet<Type> { typeof(DateTime) },
            imports: null
        );

        Assert.Equal(typeof(DateTime), provider.ResolveTypeBySimpleName("DateTime"));
    }
}
