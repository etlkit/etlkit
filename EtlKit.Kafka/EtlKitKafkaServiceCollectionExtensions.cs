using EtlKit.DataFlow;

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EtlKit.Kafka.Extensions;

/// <summary>
/// Extension methods for registering ETLBox.Kafka components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitKafkaServiceCollectionExtensions
{
    /// <summary>
    /// Registers ETLBox.Kafka data flow components as transient services using open generic registrations.
    /// </summary>
    public static IServiceCollection AddEtlBoxKafka(this IServiceCollection services)
    {
        services.AddTransient(typeof(KafkaJsonSource<>));
        services.AddTransient(typeof(KafkaStringTransformation<>));
        services.AddTransient<KafkaTransformation>();
        return services;
    }
}
