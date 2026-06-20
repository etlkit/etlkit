using EtlKit.Common.DataFlow;

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EtlKit.Json.Extensions;

/// <summary>
/// Extension methods for registering ETLBox.Json components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlBoxJsonServiceCollectionExtensions
{
    /// <summary>
    /// Registers ETLBox.Json data flow components as transient services.
    /// </summary>
    public static IServiceCollection AddEtlBoxJson(this IServiceCollection services)
    {
        services.AddTransient<JsonTransformation>();
        return services;
    }
}
