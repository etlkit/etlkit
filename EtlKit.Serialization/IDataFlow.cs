using System;
using System.Collections.Generic;
using System.Dynamic;
using EtlKit.Primitives;

namespace EtlKit.Serialization
{
    /// <summary>
    /// Data flow graph that can be executed.
    /// </summary>
    /// <remarks>
    /// This abstraction can be used when the graph is not created programmatically but rather
    /// stored in some kind of configuration file (e.g., XML).
    ///
    /// Calling convention is as follows:
    /// - Create instance of <see cref="IDataFlow"/> implementation
    /// - Initialize from XML using <see cref="System.Xml.Serialization.XmlSerializer"/>
    /// - Call Source.Execute() to start the dataflow
    /// - Wait for all destinations to complete using <see cref="IDataFlowDestination{T}.Completion"/>
    /// - Wait for all error destinations to complete using <see cref="IDataFlowDestination{T}.Completion"/>
    /// </remarks>
    public interface IDataFlow : IDisposable
    {
        /// <summary>
        /// Get or add a connection manager to the pool.
        /// </summary>
        /// <remarks>
        /// Data flow implementation owns resources for the connection manager and will dispose of it when data flow is completed.
        /// Use this method to get or add a connection manager to the pool so that other components can use it.
        /// </remarks>
        /// <param name="connectionManagerType">Connection manager type. Must implement IConnectionManager</param>
        /// <param name="key">Unique identifier of a connection manager in the pool, like connection string</param>
        /// <param name="factory">Connection manager factory</param>
        IConnectionManager GetOrAddConnectionManager(
            Type connectionManagerType,
            string? key,
            Func<Type, string?, IConnectionManager> factory
        );

        /// <summary>
        /// Single source of data transformations in the data flow.
        /// </summary>
        IDataFlowSource<ExpandoObject> Source { get; set; }

        /// <summary>
        /// All data destinations and data transformations in the flow.
        /// </summary>
        IList<IDataFlowDestination<ExpandoObject>> Destinations { get; set; }

        /// <summary>
        /// All error destinations of data flow.
        /// </summary>
        IList<IDataFlowDestination<EtlKitError>> ErrorDestinations { get; set; }
    }
}
