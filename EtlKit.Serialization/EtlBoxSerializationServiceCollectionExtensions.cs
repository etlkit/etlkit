using EtlKit.Serialization.DataFlow;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Serialization.Extensions;

/// <summary>
/// Extension methods for registering EtlKit.Serialization components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitSerializationServiceCollectionExtensions
{
    /// <summary>
    /// Registers EtlKit.Serialization data flow components as transient services.
    /// </summary>
    public static IServiceCollection AddEtlKitSerialization(this IServiceCollection services)
    {
        services.AddTransient<DataFlowXmlReader>();
        return services;
    }
}
