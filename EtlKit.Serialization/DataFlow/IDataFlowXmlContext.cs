using System;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace EtlKit.Serialization.DataFlow;

/// <summary>
/// DI-aware object factory passed to <see cref="IDataFlowXmlSerializable.ReadXml"/>.
/// Implementations must use <see cref="CreateObject"/> for all object instantiation so that
/// dependency injection is preserved across the entire pipeline.
/// </summary>
[PublicAPI]
public interface IDataFlowXmlContext
{
    /// <summary>
    /// Resolves a registered type by name without creating an instance.
    /// Returns <c>null</c> when the type is unknown — useful for source-detection checks
    /// before committing to object creation.
    /// </summary>
    Type? ResolveType(string typeName);

    /// <summary>
    /// Creates an object of the named type using the reader's activator (DI-aware).
    /// Always use this instead of <c>new</c> / <c>Activator.CreateInstance</c>.
    /// </summary>
    object? CreateObject(string typeName, XElement element);

    /// <summary>
    /// Returns a derived context in which global post-creation actions (such as universal
    /// error-link wiring) are suppressed. Use this when a component takes ownership of those
    /// actions for its children — for example, a <see cref="Pipeline"/> that propagates
    /// <c>LinkErrorTo</c> to its internal steps itself.
    /// </summary>
    IDataFlowXmlContext WithoutGlobalActions();
}
