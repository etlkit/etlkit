using System;
using JetBrains.Annotations;

namespace EtlKit.Serialization.DataFlow;

/// <summary>
/// Abstraction for creating data flow component instances during XML deserialization.
/// </summary>
[PublicAPI]
public interface IDataFlowActivator
{
    /// <summary>
    /// Creates an instance of the specified type.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>A new instance of the type, or null if creation fails.</returns>
    object? CreateInstance(Type type);
}
