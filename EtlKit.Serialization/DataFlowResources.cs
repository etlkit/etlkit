using System;
using System.Collections.Concurrent;
using EtlKit.Primitives;

namespace EtlKit.Serialization
{
    /// <summary>
    /// Embeddable helper that provides the resource-ownership implementation for <see cref="IDataFlow"/>.
    /// </summary>
    /// <remarks>
    /// Compose this into any <see cref="IDataFlow"/> implementation and delegate
    /// <see cref="IDataFlow.GetOrAddConnectionManager"/> and (optionally, by also implementing
    /// <see cref="IDataFlowResourceOwner"/>) <see cref="IDataFlowResourceOwner.GetOrAddResource"/>
    /// to it. Call <see cref="Dispose"/> from the owning object's dispose method.
    /// </remarks>
    public sealed class DataFlowResources : IDataFlowResourceOwner, IDisposable
    {
        private readonly ConcurrentDictionary<
            (Type type, string? key),
            IConnectionManager
        > _connectionManagers = new();

        private readonly ConcurrentDictionary<string, IDisposable> _resources = new();

        /// <summary>Number of connection managers currently in the pool.</summary>
        public int ConnectionManagerCount => _connectionManagers.Count;

        /// <summary>Number of disposable resources currently in the pool.</summary>
        public int ResourceCount => _resources.Count;

        /// <inheritdoc cref="IDataFlow.GetOrAddConnectionManager"/>
        public IConnectionManager GetOrAddConnectionManager(
            Type connectionManagerType,
            string? key,
            Func<Type, string?, IConnectionManager> factory
        ) =>
            _connectionManagers.GetOrAdd((connectionManagerType, key), k => factory(k.type, k.key));

        /// <inheritdoc cref="IDataFlowResourceOwner.GetOrAddResource"/>
        public IDisposable GetOrAddResource(string key, Func<IDisposable> factory) =>
            _resources.GetOrAdd(key, _ => factory());

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var value in _connectionManagers.Values)
                value.Dispose();
            _connectionManagers.Clear();
            foreach (var value in _resources.Values)
                value.Dispose();
            _resources.Clear();
        }
    }
}
