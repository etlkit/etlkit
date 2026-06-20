using System.Globalization;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Extensions;
using EtlKit.Serialization.DataFlow;
using EtlKit.Serialization.Extensions;
using CsvHelper.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests for IServiceCollection extension methods that register ETLBox components.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEtlBoxCore_ShouldRegisterOpenGenericSources()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore();

        AssertOpenGenericRegistered(services, typeof(DbSource<>));
        AssertOpenGenericRegistered(services, typeof(CsvSource<>));
        AssertOpenGenericRegistered(services, typeof(JsonSource<>));
        AssertOpenGenericRegistered(services, typeof(XmlSource<>));
        AssertOpenGenericRegistered(services, typeof(ExcelSource<>));
        AssertOpenGenericRegistered(services, typeof(MemorySource<>));
        AssertOpenGenericRegistered(services, typeof(CustomSource<>));
        AssertOpenGenericRegistered(services, typeof(CrossJoin<,,>));
    }

    [Fact]
    public void AddEtlBoxCore_ShouldRegisterOpenGenericTransformations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore();

        AssertOpenGenericRegistered(services, typeof(RowTransformation<,>));
        AssertOpenGenericRegistered(services, typeof(RowTransformation<>));
        AssertOpenGenericRegistered(services, typeof(BlockTransformation<,>));
        AssertOpenGenericRegistered(services, typeof(Multicast<>));
        AssertOpenGenericRegistered(services, typeof(Sort<>));
        AssertOpenGenericRegistered(services, typeof(RowDuplication<>));
        AssertOpenGenericRegistered(services, typeof(RowMultiplication<,>));
        AssertOpenGenericRegistered(services, typeof(Aggregation<,>));
        AssertOpenGenericRegistered(services, typeof(LookupTransformation<,>));
        AssertOpenGenericRegistered(services, typeof(MergeJoin<,,>));
        AssertOpenGenericRegistered(services, typeof(DbMerge<>));
    }

    [Fact]
    public void AddEtlBoxCore_ShouldRegisterOpenGenericDestinations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore();

        AssertOpenGenericRegistered(services, typeof(DbDestination<>));
        AssertOpenGenericRegistered(services, typeof(CsvDestination<>));
        AssertOpenGenericRegistered(services, typeof(JsonDestination<>));
        AssertOpenGenericRegistered(services, typeof(XmlDestination<>));
        AssertOpenGenericRegistered(services, typeof(MemoryDestination<>));
        AssertOpenGenericRegistered(services, typeof(CustomDestination<>));
        AssertOpenGenericRegistered(services, typeof(VoidDestination<>));
    }

    [Fact]
    public void AddEtlBoxCore_ShouldRegisterNonGenericShorthands()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore();

        AssertRegistered<DbSource>(services);
        AssertRegistered<CsvSource>(services);
        AssertRegistered<JsonSource>(services);
        AssertRegistered<XmlSource>(services);
        AssertRegistered<ExcelSource>(services);
        AssertRegistered<MemorySource>(services);
        AssertRegistered<CustomSource>(services);
        AssertRegistered<CrossJoin>(services);
        AssertRegistered<RowTransformation>(services);
        AssertRegistered<BlockTransformation>(services);
        AssertRegistered<Multicast>(services);
        AssertRegistered<Sort>(services);
        AssertRegistered<RowDuplication>(services);
        AssertRegistered<RowMultiplication>(services);
        AssertRegistered<Aggregation>(services);
        AssertRegistered<LookupTransformation>(services);
        AssertRegistered<MergeJoin>(services);
        AssertRegistered<DbMerge>(services);
        AssertRegistered<DbDestination>(services);
        AssertRegistered<CsvDestination>(services);
        AssertRegistered<JsonDestination>(services);
        AssertRegistered<XmlDestination>(services);
        AssertRegistered<MemoryDestination>(services);
        AssertRegistered<ErrorLogDestination>(services);
    }

    [Fact]
    public void AddEtlBoxCore_ShouldResolveCustomDtoType_WithoutExplicitRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore();
        var provider = services.BuildServiceProvider();

        // These types were never explicitly registered — open generics resolve them
        var source = provider.GetRequiredService<DbSource<MyCustomRow>>();
        Assert.NotNull(source);

        var dest = provider.GetRequiredService<DbDestination<MyCustomRow>>();
        Assert.NotNull(dest);

        var sort = provider.GetRequiredService<Sort<MyCustomRow>>();
        Assert.NotNull(sort);
    }

    [Fact]
    public void AddEtlBoxCore_ShouldResolveMultiTypeParamTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore();
        var provider = services.BuildServiceProvider();

        // RowTransformation<TInput, TOutput> with different input/output types
        var transform = provider.GetRequiredService<RowTransformation<MyCustomRow, AnotherRow>>();
        Assert.NotNull(transform);
    }

    [Fact]
    public void AddEtlBoxCore_ShouldInjectLoggerThroughOpenGenerics()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore();
        var provider = services.BuildServiceProvider();

        var source = provider.GetRequiredService<DbSource<MyCustomRow>>();
        Assert.NotNull(source.Logger);
    }

    [Fact]
    public void AddEtlBoxCore_ShouldRegisterAsTransient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore();

        var descriptor = services.First(d => d.ServiceType == typeof(DbSource<>));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddEtlBoxSerialization_ShouldRegisterDataFlowXmlReader()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxSerialization();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DataFlowXmlReader));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddEtlBoxCore_ShouldRegisterCsvConfigurationWithInvariantCultureByDefault()
    {
        var services = new ServiceCollection();
        services.AddEtlBoxCore();
        var provider = services.BuildServiceProvider();

        var config = provider.GetRequiredService<CsvConfiguration>();

        Assert.Equal(CultureInfo.InvariantCulture, config.CultureInfo);
    }

    [Fact]
    public void AddEtlBoxCore_ShouldRegisterCsvConfigurationWithCustomCulture()
    {
        var services = new ServiceCollection();
        var french = CultureInfo.GetCultureInfo("fr-FR");
        services.AddEtlBoxCore(csvCultureInfo: french);
        var provider = services.BuildServiceProvider();

        var config = provider.GetRequiredService<CsvConfiguration>();

        Assert.Equal(french, config.CultureInfo);
    }

    [Fact]
    public void AddEtlBoxCore_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddEtlBoxCore();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlBoxSerialization_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddEtlBoxSerialization();

        Assert.Same(services, result);
    }

    public class MyCustomRow
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    public class AnotherRow
    {
        public string Output { get; set; } = "";
    }

    private static void AssertRegistered<T>(IServiceCollection services)
    {
        Assert.True(
            services.Any(d =>
                d.ServiceType == typeof(T) && d.Lifetime == ServiceLifetime.Transient
            ),
            $"{typeof(T).Name} should be registered as transient"
        );
    }

    private static void AssertOpenGenericRegistered(
        IServiceCollection services,
        Type openGenericType
    )
    {
        Assert.True(
            services.Any(d =>
                d.ServiceType == openGenericType && d.Lifetime == ServiceLifetime.Transient
            ),
            $"{openGenericType.Name} should be registered as open generic transient"
        );
    }
}
