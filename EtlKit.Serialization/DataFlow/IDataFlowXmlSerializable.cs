using System.Xml.Linq;
using JetBrains.Annotations;

namespace EtlKit.Serialization.DataFlow;

/// <summary>
/// Allows a data-flow component to take full control of its own XML deserialization.
/// <see cref="DataFlowXmlReader"/> delegates to this interface instead of performing
/// property-by-property reflection when the created instance implements it.
/// </summary>
[PublicAPI]
public interface IDataFlowXmlSerializable
{
    /// <summary>
    /// Reads configuration and wires subcomponents from <paramref name="element"/>.
    /// Use <paramref name="context"/> for all object creation to preserve DI.
    /// </summary>
    void ReadXml(XElement element, IDataFlowXmlContext context);
}
