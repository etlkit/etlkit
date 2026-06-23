using System.Xml.Serialization;
using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    internal sealed class XmlTypeInfo : TypeInfo
    {
        internal string ElementName { get; }

        internal XmlTypeInfo(Type type)
            : base(type)
        {
            GatherTypeInfo();
            foreach (Attribute customAttribute in Attribute.GetCustomAttributes(type))
            {
                if (customAttribute is XmlRootAttribute attribute)
                {
                    ElementName = attribute.ElementName;
                }
            }
            if (string.IsNullOrWhiteSpace(ElementName))
                ElementName = type.Name;
        }
    }
}
