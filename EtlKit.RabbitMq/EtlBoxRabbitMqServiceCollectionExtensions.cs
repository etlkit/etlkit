using EtlKit.DataFlow;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EtlKit.RabbitMq.Extensions;

/// <summary>
/// Extension methods for registering EtlKit.RabbitMq components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitRabbitMqServiceCollectionExtensions
{
    /// <summary>
    /// Registers EtlKit.RabbitMq data flow components as transient services using open generic registrations.
    /// </summary>
    public static IServiceCollection AddEtlKitRabbitMq(this IServiceCollection services)
    {
        services.AddTransient(typeof(RabbitMqTransformation<,>));
        services.AddTransient<RabbitMqTransformation>();
        return services;
    }
}
