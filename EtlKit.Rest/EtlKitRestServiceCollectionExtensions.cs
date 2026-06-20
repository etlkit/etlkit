using EtlKit.Rest;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EtlKit.Rest.Extensions;

/// <summary>
/// Extension methods for registering ETLBox.Rest components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitRestServiceCollectionExtensions
{
    /// <summary>
    /// Registers ETLBox.Rest data flow components as transient services.
    /// </summary>
    public static IServiceCollection AddEtlBoxRest(this IServiceCollection services)
    {
        services.AddTransient<RestTransformation>();
        return services;
    }
}
