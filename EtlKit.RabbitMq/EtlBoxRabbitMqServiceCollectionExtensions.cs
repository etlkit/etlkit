using EtlKit.DataFlow;

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EtlKit.RabbitMq.Extensions;

/// <summary>
/// Extension methods for registering ETLBox.RabbitMq components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlBoxRabbitMqServiceCollectionExtensions
{
    /// <summary>
    /// Registers ETLBox.RabbitMq data flow components as transient services using open generic registrations.
    /// </summary>
    public static IServiceCollection AddEtlBoxRabbitMq(this IServiceCollection services)
    {
        services.AddTransient(typeof(RabbitMqTransformation<,>));
        services.AddTransient<RabbitMqTransformation>();
        return services;
    }
}
