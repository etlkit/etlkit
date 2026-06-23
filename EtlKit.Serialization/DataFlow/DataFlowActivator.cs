using System;
using JetBrains.Annotations;

namespace EtlKit.Serialization.DataFlow;

/// <summary>
/// Static helper for creating data flow component instances.
/// </summary>
[PublicAPI]
[Obsolete(
    "Use IDataFlowActivator implementations (DefaultDataFlowActivator or ServiceProviderActivator) instead."
)]
public static class DataFlowActivator
{
    private static readonly DefaultDataFlowActivator Default = new();

    /// <summary>
    /// Creates an instance of the specified type using the default activator.
    /// </summary>
    public static object? CreateInstance(Type type) => Default.CreateInstance(type);
}
