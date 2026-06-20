using System;
using JetBrains.Annotations;

namespace EtlKit.Serialization.DataFlow
{
    /// <summary>
    /// Optional capability an <see cref="IDataFlowActivator"/> may provide to report whether the
    /// instances it creates for a given type are owned by an external lifetime scope (e.g. a DI
    /// container) rather than by the data flow.
    /// </summary>
    /// <remarks>
    /// The XML reader uses this to decide disposal ownership of <see cref="IDisposable"/> properties:
    /// instances created fresh by the activator (e.g. <see cref="DefaultDataFlowActivator"/>) are
    /// owned by the data flow and registered for disposal; instances resolved from an external
    /// container (e.g. <see cref="ServiceProviderActivator"/>) are owned by that container and must
    /// NOT be disposed by the data flow. Activators that do not implement this interface are treated
    /// as always creating flow-owned instances (the default, backward-compatible behavior).
    /// </remarks>
    [PublicAPI]
    public interface ILifetimeAwareActivator
    {
        /// <summary>
        /// Returns <c>true</c> if instances of <paramref name="type"/> created by this activator are
        /// owned by an external lifetime scope (such as a DI container) and therefore must not be
        /// disposed by the data flow.
        /// </summary>
        bool IsExternallyOwned(Type type);
    }
}
