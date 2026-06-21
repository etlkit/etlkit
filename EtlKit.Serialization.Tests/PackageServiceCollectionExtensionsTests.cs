using EtlKit.AI;
using EtlKit.AI.Extensions;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Json.Extensions;
using EtlKit.Kafka.Extensions;
using EtlKit.RabbitMq.Extensions;
using EtlKit.Rest;
using EtlKit.Rest.Extensions;
using EtlKit.Scripting;
using EtlKit.Scripting.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests for package-specific IServiceCollection extension methods.
/// </summary>
public class PackageServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEtlKitAI_ShouldRegisterAIBatchTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitAI();

        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(AIBatchTransformation)
        );
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlKitAI_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlKitAI();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlKitAI_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitAI();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<AIBatchTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlKitJson_ShouldRegisterJsonTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitJson();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(JsonTransformation));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlKitJson_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlKitJson();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlKitJson_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitJson();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<JsonTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlKitKafka_ShouldRegisterOpenGenericKafkaJsonSource()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitKafka();

        Assert.True(
            services.Any(d =>
                d.ServiceType == typeof(KafkaJsonSource<>)
                && d.Lifetime == ServiceLifetime.Transient
            ),
            "KafkaJsonSource<> should be registered as open generic transient"
        );
    }

    [Fact]
    public void AddEtlKitKafka_ShouldRegisterOpenGenericKafkaStringTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitKafka();

        Assert.True(
            services.Any(d =>
                d.ServiceType == typeof(KafkaStringTransformation<>)
                && d.Lifetime == ServiceLifetime.Transient
            ),
            "KafkaStringTransformation<> should be registered as open generic transient"
        );
    }

    [Fact]
    public void AddEtlKitKafka_ShouldRegisterNonGenericKafkaTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitKafka();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(KafkaTransformation));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlKitKafka_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlKitKafka();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlKitKafka_ShouldResolveKafkaTransformationWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitKafka();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<KafkaTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlKitRabbitMq_ShouldRegisterOpenGenericRabbitMqTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitRabbitMq();

        Assert.True(
            services.Any(d =>
                d.ServiceType == typeof(RabbitMqTransformation<,>)
                && d.Lifetime == ServiceLifetime.Transient
            ),
            "RabbitMqTransformation<,> should be registered as open generic transient"
        );
    }

    [Fact]
    public void AddEtlKitRabbitMq_ShouldRegisterNonGenericRabbitMqTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitRabbitMq();

        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(RabbitMqTransformation)
        );
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlKitRabbitMq_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlKitRabbitMq();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlKitRabbitMq_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitRabbitMq();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<RabbitMqTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlKitRest_ShouldRegisterRestTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitRest();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(RestTransformation));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlKitRest_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlKitRest();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlKitRest_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitRest();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<RestTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlKitScripting_ShouldRegisterScriptedTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitScripting();

        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ScriptedTransformation)
        );
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlKitScripting_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlKitScripting();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlKitScripting_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitScripting();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<ScriptedTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AllExtensions_ShouldCombineWithoutConflicts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitAI();
        services.AddEtlKitJson();
        services.AddEtlKitKafka();
        services.AddEtlKitRabbitMq();
        services.AddEtlKitRest();
        services.AddEtlKitScripting();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<AIBatchTransformation>());
        Assert.NotNull(provider.GetRequiredService<JsonTransformation>());
        Assert.NotNull(provider.GetRequiredService<KafkaTransformation>());
        Assert.NotNull(provider.GetRequiredService<RabbitMqTransformation>());
        Assert.NotNull(provider.GetRequiredService<RestTransformation>());
        Assert.NotNull(provider.GetRequiredService<ScriptedTransformation>());
    }
}
