using System.Dynamic;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EtlKit.DataFlow;
using EtlKit.DynamicLinq;
using EtlKit.Serialization.DataFlow;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// XML deserialization tests for ExpressionRowFiltration via DataFlowXmlReader,
/// confirming production usage in XML-defined ETL packages.
/// </summary>
public class ExpressionRowFiltrationDeserializationTests
{
    [Fact]
    public void ExpressionRowFiltration_XmlDeserialization_UnescapesXmlSpecialCharacters()
    {
        // FilterExpression is a free-form string; XML special characters must be escaped
        // in the source XML and unescaped on read. Single test covers >, <, &&, " escapes
        // together — what production XML packages need to round-trip.
        var xml =
            @"<ExpressionRowFiltration>
                <FilterExpression>Order.Total &gt; 100 &amp;&amp; Type != &quot;Recalculation&quot;</FilterExpression>
              </ExpressionRowFiltration>";

        var result = Deserialize(xml);

        Assert.NotNull(result);
        Assert.Equal("Order.Total > 100 && Type != \"Recalculation\"", result.FilterExpression);
    }

    [Fact]
    public void ExpressionRowFiltration_XmlDeserialization_EmptyFilterExpression_IsAccepted()
    {
        // Validation of empty FilterExpression happens at runtime (per-row), not at deserialization time.
        var xml =
            @"<ExpressionRowFiltration>
                <FilterExpression></FilterExpression>
              </ExpressionRowFiltration>";

        var result = Deserialize(xml);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.FilterExpression);
    }

    [Fact]
    public void ExpressionRowFiltration_FullXmlPipeline_PassesMatchingRowDropsOther()
    {
        using var step = DeserializeStep(BuildPipelineXml("Reserve &gt; 0"));
        var source = (MemorySource<ExpandoObject>)step.Source;
        source.DataAsList.Add(MakeRow(("Reserve", 100m))); // passes
        source.DataAsList.Add(MakeRow(("Reserve", -50m))); // dropped

        step.Invoke(CancellationToken.None);

        var dest = (MemoryDestination<ExpandoObject>)step.Destinations[0];
        var passed = Assert.Single(dest.Data);
        Assert.Equal(100m, ((IDictionary<string, object?>)passed)["Reserve"]);
    }

    [Fact]
    public void ExpressionRowFiltration_FullXmlPipeline_EmptySource()
    {
        using var step = DeserializeStep(BuildPipelineXml("Reserve &gt; 0"));

        step.Invoke(CancellationToken.None);

        var dest = (MemoryDestination<ExpandoObject>)step.Destinations[0];
        Assert.Empty(dest.Data);
    }

    [Fact]
    public void ExpressionRowFiltration_XmlDeserialization_AdditionalAssemblyNames_RoundTrip()
    {
        // AdditionalAssemblyNames is the headline Phase 1 feature for XML-defined
        // flows (closes XML-flow user-type case from review note 84488). Verify
        // that <AdditionalAssemblyNames><string>...</string></...> populates the
        // property as written and the assembly resolution succeeds during the
        // setter. System.Linq is used as a known-loaded contract assembly.
        var xml =
            @"<ExpressionRowFiltration>
                <FilterExpression>true</FilterExpression>
                <AdditionalAssemblyNames>
                    <string>System.Linq</string>
                </AdditionalAssemblyNames>
              </ExpressionRowFiltration>";

        var result = Deserialize(xml);

        Assert.NotNull(result);
        Assert.Equal(new[] { "System.Linq" }, result.AdditionalAssemblyNames);
    }

    [Fact]
    public void ExpressionRowFiltration_XmlDeserialization_AdditionalImports_RoundTrip()
    {
        // AdditionalImports is the namespace-prefix companion to AdditionalAssemblyNames.
        // Pure round-trip - no assembly load happens for imports, so we can use any
        // namespace-shaped string.
        var xml =
            @"<ExpressionRowFiltration>
                <FilterExpression>true</FilterExpression>
                <AdditionalImports>
                    <string>System.Globalization</string>
                    <string>MyCompany.Domain</string>
                </AdditionalImports>
              </ExpressionRowFiltration>";

        var result = Deserialize(xml);

        Assert.NotNull(result);
        Assert.Equal(
            new[] { "System.Globalization", "MyCompany.Domain" },
            result.AdditionalImports
        );
    }

    [Fact]
    public void ExpressionRowFiltration_XmlDeserialization_MixedPropertyOrder_LazyRebuildSucceeds()
    {
        // Property setters fire in document order during XmlSerializer-style
        // deserialization. Round 8 made type-provider rebuild lazy specifically
        // because that order is not guaranteed across XML packages. Verify that
        // an arbitrary order (Imports before FilterExpression, AssemblyNames after)
        // still produces a working filtration with the type provider correctly
        // installed on the first row evaluation (review note 84548).
        var xml =
            @"<ExpressionRowFiltration>
                <AdditionalImports>
                    <string>System.Linq</string>
                </AdditionalImports>
                <FilterExpression>Reserve &gt; 0</FilterExpression>
                <AdditionalAssemblyNames>
                    <string>System.Linq</string>
                </AdditionalAssemblyNames>
              </ExpressionRowFiltration>";

        var result = Deserialize(xml);

        Assert.NotNull(result);
        Assert.Single(result.AdditionalImports);
        Assert.Single(result.AdditionalAssemblyNames);

        // Trigger first-row evaluation - lazy RebuildTypeProvider must run here
        // and install DynamicLinqTypeProvider into ParsingConfig.
        Assert.True(result.PredicateFunc!(MakeRow(("Reserve", 100m))));
        Assert.False(result.PredicateFunc!(MakeRow(("Reserve", -1m))));

        Assert.IsType<DynamicLinqTypeProvider>(result.ParsingConfig.CustomTypeProvider);
    }

    private static string BuildPipelineXml(string filterExpression) =>
        $@"<EtlDataFlowStep>
            <MemorySource>
                <LinkTo>
                    <ExpressionRowFiltration>
                        <FilterExpression>{filterExpression}</FilterExpression>
                        <LinkTo>
                            <MemoryDestination />
                        </LinkTo>
                    </ExpressionRowFiltration>
                </LinkTo>
            </MemorySource>
          </EtlDataFlowStep>";

    private static EtlDataFlowStep DeserializeStep(string xml)
    {
        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        var step = new EtlDataFlowStep();
        new DataFlowXmlReader(step).Read(xmlReader);
        return step;
    }

    private static ExpressionRowFiltration? Deserialize(string xml)
    {
        var element = XElement.Parse(xml);
        using var mockDataFlow = new EtlDataFlowStep();
        var reader = new DataFlowXmlReader(mockDataFlow);
        var createObject = typeof(DataFlowXmlReader).GetMethod(
            "CreateObject",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        return createObject!.Invoke(reader, [typeof(ExpressionRowFiltration), element])
            as ExpressionRowFiltration;
    }

    private static ExpandoObject MakeRow(params (string key, object value)[] fields)
    {
        var row = new ExpandoObject();
        var dict = (IDictionary<string, object?>)row;
        foreach (var (key, value) in fields)
            dict[key] = value;
        return row;
    }
}
