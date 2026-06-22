using System.Dynamic;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests that ILogger&lt;T&gt; constructors work correctly on concrete data flow classes.
/// Verifies the logger is properly stored and used instead of the static LoggerFactory fallback.
/// </summary>
public class LoggerInjectionTests
{
    [Fact]
    public void DbSource_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<DbSource<ExpandoObject>>>();

        var source = new DbSource<ExpandoObject>(logger);

        Assert.Same(logger, source.Logger);
    }

    [Fact]
    public void DbSource_NonGeneric_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<DbSource>>();

        var source = new DbSource(logger);

        Assert.Same(logger, source.Logger);
    }

    [Fact]
    public void DbDestination_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<DbDestination<ExpandoObject>>>();

        var dest = new DbDestination<ExpandoObject>(logger);

        Assert.Same(logger, dest.Logger);
    }

    [Fact]
    public void RowTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<
            ILogger<RowTransformation<ExpandoObject, ExpandoObject>>
        >();

        var transform = new RowTransformation<ExpandoObject, ExpandoObject>(logger);

        Assert.Same(logger, transform.Logger);
    }

    [Fact]
    public void RowTransformation_SingleGeneric_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<RowTransformation<ExpandoObject>>>();

        var transform = new RowTransformation<ExpandoObject>(logger);

        Assert.Same(logger, transform.Logger);
    }

    [Fact]
    public void RowTransformation_NonGeneric_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<RowTransformation>>();

        var transform = new RowTransformation(logger);

        Assert.Same(logger, transform.Logger);
    }

    [Fact]
    public void MemorySource_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<MemorySource<ExpandoObject>>>();

        var source = new MemorySource<ExpandoObject>(logger);

        Assert.Same(logger, source.Logger);
        Assert.NotNull(source.Data); // init code should still run
    }

    [Fact]
    public void MemoryDestination_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<MemoryDestination<ExpandoObject>>>();

        var dest = new MemoryDestination<ExpandoObject>(logger);

        Assert.Same(logger, dest.Logger);
    }

    [Fact]
    public void CustomDestination_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<CustomDestination<ExpandoObject>>>();

        var dest = new CustomDestination<ExpandoObject>(logger);

        Assert.Same(logger, dest.Logger);
    }

    [Fact]
    public void CsvSource_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<CsvSource<ExpandoObject>>>();

        var source = new CsvSource<ExpandoObject>(logger);

        Assert.Same(logger, source.Logger);
    }

    [Fact]
    public void JsonSource_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<JsonSource<ExpandoObject>>>();

        var source = new JsonSource<ExpandoObject>(logger);

        Assert.Same(logger, source.Logger);
    }

    [Fact]
    public void Sort_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<Sort<ExpandoObject>>>();

        var sort = new Sort<ExpandoObject>(logger);

        Assert.Same(logger, sort.Logger);
    }

    [Fact]
    public void Multicast_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<Multicast<ExpandoObject>>>();

        var multicast = new Multicast<ExpandoObject>(logger);

        Assert.Same(logger, multicast.Logger);
    }

    [Fact]
    public void VoidDestination_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<VoidDestination<ExpandoObject>>>();

        var dest = new VoidDestination<ExpandoObject>(logger);

        Assert.Same(logger, dest.Logger);
    }

    [Fact]
    public void ErrorLogDestination_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<ErrorLogDestination>>();

        var dest = new ErrorLogDestination(logger);

        Assert.Same(logger, dest.Logger);
    }

    [Fact]
    public void Aggregation_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<
            ILogger<Aggregation<ExpandoObject, ExpandoObject>>
        >();

        var agg = new Aggregation<ExpandoObject, ExpandoObject>(logger);

        Assert.Same(logger, agg.Logger);
    }

    [Fact]
    public void BlockTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<
            ILogger<BlockTransformation<ExpandoObject, ExpandoObject>>
        >();

        var block = new BlockTransformation<ExpandoObject, ExpandoObject>(logger);

        Assert.Same(logger, block.Logger);
    }

    [Fact]
    public void Component_WithoutLogger_ShouldFallbackToStaticLoggerFactory()
    {
        // Without passing a logger, the component should still work
        // (using the static ControlFlow.LoggerFactory fallback)
        var source = new MemorySource<ExpandoObject>();

        Assert.NotNull(source.Logger);
    }

    [Fact]
    public void DI_ShouldResolveComponentWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<MemorySource>();
        var provider = services.BuildServiceProvider();

        var source = provider.GetRequiredService<MemorySource>();

        Assert.NotNull(source);
    }
}
