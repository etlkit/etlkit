using EtlKit.AI.Extensions;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Json.Extensions;
using EtlKit.Kafka.Extensions;
using EtlKit.RabbitMq.Extensions;
using EtlKit.Rest.Extensions;
using EtlKit.Scripting;
using EtlKit.Scripting.Extensions;
using EtlKit.AI;
using EtlKit.Rest;

using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests for package-specific IServiceCollection extension methods.
/// </summary>
public class PackageServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEtlBoxAI_ShouldRegisterAIBatchTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxAI();

        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(AIBatchTransformation)
        );
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlBoxAI_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlBoxAI();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlBoxAI_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxAI();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<AIBatchTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlBoxJson_ShouldRegisterJsonTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxJson();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(JsonTransformation));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlBoxJson_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlBoxJson();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlBoxJson_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxJson();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<JsonTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlBoxKafka_ShouldRegisterOpenGenericKafkaJsonSource()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxKafka();

        Assert.True(
            services.Any(d =>
                d.ServiceType == typeof(KafkaJsonSource<>)
                && d.Lifetime == ServiceLifetime.Transient
            ),
            "KafkaJsonSource<> should be registered as open generic transient"
        );
    }

    [Fact]
    public void AddEtlBoxKafka_ShouldRegisterOpenGenericKafkaStringTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxKafka();

        Assert.True(
            services.Any(d =>
                d.ServiceType == typeof(KafkaStringTransformation<>)
                && d.Lifetime == ServiceLifetime.Transient
            ),
            "KafkaStringTransformation<> should be registered as open generic transient"
        );
    }

    [Fact]
    public void AddEtlBoxKafka_ShouldRegisterNonGenericKafkaTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxKafka();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(KafkaTransformation));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlBoxKafka_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlBoxKafka();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlBoxKafka_ShouldResolveKafkaTransformationWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxKafka();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<KafkaTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlBoxRabbitMq_ShouldRegisterOpenGenericRabbitMqTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxRabbitMq();

        Assert.True(
            services.Any(d =>
                d.ServiceType == typeof(RabbitMqTransformation<,>)
                && d.Lifetime == ServiceLifetime.Transient
            ),
            "RabbitMqTransformation<,> should be registered as open generic transient"
        );
    }

    [Fact]
    public void AddEtlBoxRabbitMq_ShouldRegisterNonGenericRabbitMqTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxRabbitMq();

        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(RabbitMqTransformation)
        );
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlBoxRabbitMq_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlBoxRabbitMq();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlBoxRabbitMq_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxRabbitMq();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<RabbitMqTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlBoxRest_ShouldRegisterRestTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxRest();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(RestTransformation));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlBoxRest_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlBoxRest();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlBoxRest_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxRest();
        var provider = services.BuildServiceProvider();

        var component = provider.GetRequiredService<RestTransformation>();

        Assert.NotNull(component);
        Assert.NotNull(component.Logger);
    }

    [Fact]
    public void AddEtlBoxScripting_ShouldRegisterScriptedTransformation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxScripting();

        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ScriptedTransformation)
        );
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddEtlBoxScripting_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddEtlBoxScripting();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtlBoxScripting_ShouldResolveWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxScripting();
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
        services.AddEtlBoxAI();
        services.AddEtlBoxJson();
        services.AddEtlBoxKafka();
        services.AddEtlBoxRabbitMq();
        services.AddEtlBoxRest();
        services.AddEtlBoxScripting();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<AIBatchTransformation>());
        Assert.NotNull(provider.GetRequiredService<JsonTransformation>());
        Assert.NotNull(provider.GetRequiredService<KafkaTransformation>());
        Assert.NotNull(provider.GetRequiredService<RabbitMqTransformation>());
        Assert.NotNull(provider.GetRequiredService<RestTransformation>());
        Assert.NotNull(provider.GetRequiredService<ScriptedTransformation>());
    }
}
