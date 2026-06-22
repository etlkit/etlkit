using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.DynamicLinq.Extensions;

/// <summary>
/// Extension methods for registering EtlKit.DynamicLinq components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitDynamicLinqServiceCollectionExtensions
{
    /// <summary>
    /// Registers EtlKit.DynamicLinq data flow components as transient services.
    /// Both the non-generic <see cref="ExpressionRowFiltration"/> (ExpandoObject
    /// rows) and the open generic <see cref="ExpressionRowFiltration{TInput}"/>
    /// are registered, so callers can resolve typed instances such as
    /// <c>ExpressionRowFiltration&lt;Order&gt;</c> directly from the container.
    /// </summary>
    public static IServiceCollection AddEtlKitDynamicLinq(this IServiceCollection services)
    {
        services.AddTransient<ExpressionRowFiltration>();
        services.AddTransient(typeof(ExpressionRowFiltration<>));
        return services;
    }
}
