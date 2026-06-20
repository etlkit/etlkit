using System;
using System.Dynamic;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Serialization.DataFlow;

/// <summary>
/// Activator that uses an <see cref="IServiceProvider"/> to create instances.
/// First tries to resolve from the container (respecting registered lifetimes),
/// then falls back to <see cref="ActivatorUtilities.CreateInstance"/> for unregistered types.
/// </summary>
[PublicAPI]
public class ServiceProviderActivator : IDataFlowActivator, ILifetimeAwareActivator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates a new instance of <see cref="ServiceProviderActivator"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider to use for resolving instances.</param>
    public ServiceProviderActivator(IServiceProvider serviceProvider)
    {
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public object? CreateInstance(Type type)
    {
        var constructedType = type;
        if (type.IsGenericType && type.IsGenericTypeDefinition)
        {
            constructedType = type.MakeGenericType(typeof(ExpandoObject));
        }

        // First try to resolve from the container to respect registered lifetimes
        // (Transient/Scoped/Singleton). Fall back to ActivatorUtilities.CreateInstance
        // for types not registered in the container.
        return _serviceProvider.GetService(constructedType)
            ?? ActivatorUtilities.CreateInstance(_serviceProvider, constructedType);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A type is externally owned when it is registered in the container — then
    /// <see cref="CreateInstance"/> resolves it via <c>GetService</c> and the container manages its
    /// lifetime (and disposes it). Unregistered types are created fresh via
    /// <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/> and are
    /// owned by the caller (the data flow).
    /// </remarks>
    public bool IsExternallyOwned(Type type)
    {
        if (type is null)
            return false;

        var constructedType = type;
        if (type.IsGenericType && type.IsGenericTypeDefinition)
        {
            constructedType = type.MakeGenericType(typeof(ExpandoObject));
        }

        var isService = _serviceProvider.GetService<IServiceProviderIsService>();
        return isService?.IsService(constructedType) ?? false;
    }
}
