using System.Dynamic;
using System.Text;
using System.Xml.Serialization;
using EtlKit.DataFlow;

namespace EtlKit.Serialization.Tests;

public class ScriptedTransformationXmlTests
{
    /// <summary>
    /// Verifies a two-component pipeline configured entirely via XML:
    /// 1. JsonTransformation with PassThrough=true parses the Payload JSON string field
    ///    into a native-typed ExpandoObject (JsonContext) using Path="$".
    /// 2. ScriptedTransformation with PassThrough=true extracts individual fields from
    ///    JsonContext using plain property access — no GetString()/GetInt32() boilerplate.
    /// </summary>
    [Fact]
    public void JsonTransformation_ChainedWithScriptedTransformation_ExtractsNativeTypedFields()
    {
        const string xml = """
            <EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <JsonTransformation>
                            <PassThrough>true</PassThrough>
                            <Mappings>
                                <JsonContext>
                                    <Name>Payload</Name>
                                    <Path>$</Path>
                                </JsonContext>
                            </Mappings>
                            <LinkTo>
                                <ScriptedTransformation>
                                    <PassThrough>true</PassThrough>
                                    <Mappings>
                                        <Name>JsonContext.Name</Name>
                                        <Score>JsonContext.Score</Score>
                                        <ParsedDate>JsonContext.Date</ParsedDate>
                                        <ProcessedAt>DateTime.UtcNow</ProcessedAt>
                                    </Mappings>
                                    <LinkTo>
                                        <MemoryDestination />
                                    </LinkTo>
                                </ScriptedTransformation>
                            </LinkTo>
                        </JsonTransformation>
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>
            """;

        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        var serializer = new XmlSerializer(typeof(EtlDataFlowStep));
        using var step = (EtlDataFlowStep)serializer.Deserialize(stream)!;

        var source = (MemorySource)step.Source;
        var item = new ExpandoObject() as IDictionary<string, object?>;
        item["Payload"] = """{"Name":"Alice","Score":100,"Date":"2024-01-15T10:00:00"}""";
        source.DataAsList.Add((ExpandoObject)item);

        // Act
        step.Invoke(CancellationToken.None);

        // Assert
        var dest = (MemoryDestination)step.Destinations[0];
        Assert.Single(dest.Data);
        var result = (IDictionary<string, object?>)dest.Data.First();

        // Native types extracted directly — no JsonElement casting needed
        Assert.Equal("Alice", result["Name"]);
        Assert.Equal(100, result["Score"]);
        Assert.Equal(
            new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Unspecified),
            result["ParsedDate"]
        );
        Assert.IsType<DateTime>(result["ProcessedAt"]);

        // PassThrough preserved both the original Payload and the intermediate JsonContext
        Assert.IsType<string>(result["Payload"]);
        Assert.IsType<ExpandoObject>(result["JsonContext"]);
    }
}
