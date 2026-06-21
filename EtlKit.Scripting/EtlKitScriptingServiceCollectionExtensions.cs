using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EtlKit.Scripting.Extensions;

/// <summary>
/// Extension methods for registering EtlKit.Scripting components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitScriptingServiceCollectionExtensions
{
    /// <summary>
    /// Registers EtlKit.Scripting data flow components as transient services.
    /// </summary>
    public static IServiceCollection AddEtlKitScripting(this IServiceCollection services)
    {
        services.AddTransient<ScriptedTransformation>();
        return services;
    }
}
