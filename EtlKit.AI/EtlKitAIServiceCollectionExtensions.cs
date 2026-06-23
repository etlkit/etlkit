using EtlKit.AI;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.AI.Extensions;

/// <summary>
/// Extension methods for registering EtlKit.AI components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers EtlKit.AI data flow components as transient services.
    /// </summary>
    public static IServiceCollection AddEtlKitAI(this IServiceCollection services)
    {
        services.AddTransient<AIBatchTransformation>();
        return services;
    }
}
