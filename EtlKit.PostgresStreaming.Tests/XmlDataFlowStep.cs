using System.Dynamic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using EtlKit.Primitives;
using EtlKit.Serialization;
using EtlKit.Serialization.DataFlow;

namespace EtlKit.PostgresStreaming.Tests;

/// <summary>
/// Minimal <see cref="IDataFlow"/> host used to run a pipeline defined entirely in XML, standing in
/// for the ETL runtime that executes such packages in production.
/// </summary>
public sealed class XmlDataFlowStep : IDataFlow, IDataFlowResourceOwner, IXmlSerializable
{
    private readonly DataFlowResources _resources = new();

    public Guid? ReferenceId { get; set; }

    public string? Name { get; set; }

    public int? TimeoutMilliseconds { get; set; }

    public IDataFlowSource<ExpandoObject> Source { get; set; } = null!;

    public IList<IDataFlowDestination<ExpandoObject>> Destinations { get; set; } = null!;

    public IList<IDataFlowDestination<EtlKitError>> ErrorDestinations { get; set; } = null!;

    public IConnectionManager GetOrAddConnectionManager(
        Type connectionManagerType,
        string? key,
        Func<Type, string?, IConnectionManager> factory
    ) => _resources.GetOrAddConnectionManager(connectionManagerType, key, factory);

    public IDisposable GetOrAddResource(string key, Func<IDisposable> factory) =>
        _resources.GetOrAddResource(key, factory);

    public XmlSchema? GetSchema() => null;

    public void ReadXml(XmlReader reader) => new DataFlowXmlReader(this).Read(reader);

    public void WriteXml(XmlWriter writer) => throw new NotSupportedException();

    public void Invoke(CancellationToken cancellationToken)
    {
        Source.Execute(cancellationToken);
        var completions = Destinations
            .Select(d => d.Completion)
            .Concat(ErrorDestinations.Select(e => e.Completion))
            .ToArray();
        Task.WaitAll(completions, CancellationToken.None);
    }

    public void Dispose() => _resources.Dispose();
}
