using EtlKit.Common.DataFlow;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EtlKit.Json.Extensions;

/// <summary>
/// Extension methods for registering EtlKit.Json components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitJsonServiceCollectionExtensions
{
    /// <summary>
    /// Registers EtlKit.Json data flow components as transient services.
    /// </summary>
    public static IServiceCollection AddEtlKitJson(this IServiceCollection services)
    {
        services.AddTransient<JsonTransformation>();
        return services;
    }
}
