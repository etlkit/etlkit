using System;
using EtlKit.Primitives;
using JetBrains.Annotations;

namespace EtlKit.Serialization
{
    /// <summary>
    /// Optional capability that an <see cref="IDataFlow"/> implementation may provide to own and
    /// deduplicate the flow's disposable resources: both <see cref="IConnectionManager"/>s (via
    /// <see cref="GetOrAddConnectionManager"/>) and arbitrary <see cref="IDisposable"/> resources such
    /// as non-ADO.NET clients — <c>MongoClient</c>, <c>HttpClient</c> — that have no natural connection
    /// manager (via <see cref="GetOrAddResource"/>). It is the contract implemented by the reusable
    /// <see cref="DataFlowResources"/> helper.
    /// </summary>
    /// <remarks>
    /// This is a <b>separate, optional</b> capability interface — exposing <see cref="GetOrAddResource"/>
    /// here rather than directly on <see cref="IDataFlow"/> avoids binary-breaking existing external
    /// <see cref="IDataFlow"/> implementations compiled against earlier EtlKit versions. The XML reader
    /// probes for this capability via a type check and falls back to plain instance creation (without
    /// dedup/ownership) when the data flow does not implement it.
    /// <para>
    /// <see cref="GetOrAddConnectionManager"/> is declared here too, so the resource-ownership contract
    /// is complete — one place owns every kind of flow resource. The same method also lives on
    /// <see cref="IDataFlow"/> (where it always has, for backward compatibility); a data flow that
    /// implements both interfaces satisfies them with a single method.
    /// </para>
    /// </remarks>
    [PublicAPI]
    public interface IDataFlowResourceOwner
    {
        /// <summary>
        /// Gets or adds a connection manager to the flow's ownership pool. Mirrors
        /// <see cref="IDataFlow.GetOrAddConnectionManager"/>: connection managers sharing the same
        /// <paramref name="connectionManagerType"/> and <paramref name="key"/> are deduplicated and
        /// disposed together with the flow.
        /// </summary>
        /// <param name="connectionManagerType">Connection manager type. Must implement <see cref="IConnectionManager"/>.</param>
        /// <param name="key">Deduplication key within the pool, such as the connection string.</param>
        /// <param name="factory">Creates the connection manager when no entry for the key exists yet.</param>
        IConnectionManager GetOrAddConnectionManager(
            Type connectionManagerType,
            string? key,
            Func<Type, string?, IConnectionManager> factory
        );

        /// <summary>
        /// Gets or adds a disposable resource to the flow's ownership pool.
        /// </summary>
        /// <remarks>
        /// The data flow owns resources added via this method and disposes them when the data flow
        /// itself is disposed. Resources with identical <paramref name="key"/> values are deduplicated —
        /// subsequent calls with the same key return the existing instance without invoking
        /// <paramref name="factory"/>. Only resources whose lifetime the data flow owns should be added
        /// here; externally managed resources (e.g. resolved from a DI container) must not be.
        /// </remarks>
        /// <param name="key">
        /// Deduplication key. Resources that share a key share an instance. A typical key is the
        /// type's full name combined with a serialized representation of its configuration.
        /// </param>
        /// <param name="factory">
        /// Creates the resource when no entry for <paramref name="key"/> exists yet.
        /// </param>
        IDisposable GetOrAddResource(string key, Func<IDisposable> factory);
    }

    /// <summary>
    /// Extension methods for <see cref="IDataFlowResourceOwner"/>.
    /// </summary>
    public static class DataFlowResourceOwnerExtensions
    {
        /// <summary>
        /// Gets or adds a typed disposable resource to the flow's ownership pool.
        /// </summary>
        /// <typeparam name="T">The concrete resource type.</typeparam>
        /// <param name="owner">The data flow that owns the resource.</param>
        /// <param name="key">Deduplication key.</param>
        /// <param name="factory">Creates the resource if no entry for <paramref name="key"/> exists yet.</param>
        public static T GetOrAddResource<T>(
            this IDataFlowResourceOwner owner,
            string key,
            Func<T> factory
        )
            where T : IDisposable => (T)owner.GetOrAddResource(key, () => factory());
    }
}
