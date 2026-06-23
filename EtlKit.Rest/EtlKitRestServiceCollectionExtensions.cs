using EtlKit.Rest;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EtlKit.Rest.Extensions;

/// <summary>
/// Extension methods for registering EtlKit.Rest components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitRestServiceCollectionExtensions
{
    /// <summary>
    /// Registers EtlKit.Rest data flow components as transient services.
    /// </summary>
    public static IServiceCollection AddEtlKitRest(this IServiceCollection services)
    {
        services.AddTransient<RestTransformation>();
        return services;
    }
}
