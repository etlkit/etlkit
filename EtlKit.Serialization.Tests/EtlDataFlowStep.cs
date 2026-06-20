using System.Dynamic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using EtlKit.Serialization.DataFlow;
using EtlKit.Primitives;

namespace EtlKit.Serialization.Tests
{
    [Serializable]
    public class EtlDataFlowStep : IDataFlow, IDataFlowResourceOwner, IXmlSerializable
    {
        private readonly DataFlowResources _resources = new();

        public Guid? ReferenceId { get; set; }

        public string? Name { get; set; }

        public int? TimeoutMilliseconds { get; set; }

        public IConnectionManager GetOrAddConnectionManager(
            Type connectionManagerType,
            string? key,
            Func<Type, string?, IConnectionManager> factory
        ) => _resources.GetOrAddConnectionManager(connectionManagerType, key, factory);

        public IDataFlowSource<ExpandoObject> Source { get; set; } = null!;

        public IList<IDataFlowDestination<ExpandoObject>> Destinations { get; set; } = null!;

        public IList<IDataFlowDestination<EtlKitError>> ErrorDestinations { get; set; } = null!;

        public XmlSchema? GetSchema() => null;

        public virtual void ReadXml(XmlReader reader)
        {
            var xmlReader = new DataFlowXmlReader(this);
            xmlReader.Read(reader);
        }

        public void WriteXml(XmlWriter writer)
        {
            throw new NotSupportedException();
        }

        public void Invoke(CancellationToken cancellationToken)
        {
            Source.Execute(cancellationToken);
            var tasks = Destinations
                .Select(d => d.Completion)
                .Concat(ErrorDestinations.Select(ed => ed.Completion))
                .ToArray();
            Task.WaitAll(tasks, CancellationToken.None);
        }

        public IDisposable GetOrAddResource(string key, Func<IDisposable> factory) =>
            _resources.GetOrAddResource(key, factory);

        /// <summary>
        /// Method for checking connection managers added for disposal.
        /// </summary>
        public int ConnectionManagerCount() => _resources.ConnectionManagerCount;

        /// <summary>
        /// Method for checking disposable resources added for disposal.
        /// </summary>
        public int ResourceCount() => _resources.ResourceCount;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Violates rule
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;
            _resources.Dispose();
        }
    }
}
