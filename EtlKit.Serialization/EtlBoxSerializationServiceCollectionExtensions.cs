using EtlKit.Serialization.DataFlow;

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Serialization.Extensions;

/// <summary>
/// Extension methods for registering ETLBox.Serialization components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlBoxSerializationServiceCollectionExtensions
{
    /// <summary>
    /// Registers ETLBox.Serialization data flow components as transient services.
    /// </summary>
    public static IServiceCollection AddEtlBoxSerialization(this IServiceCollection services)
    {
        services.AddTransient<DataFlowXmlReader>();
        return services;
    }
}
