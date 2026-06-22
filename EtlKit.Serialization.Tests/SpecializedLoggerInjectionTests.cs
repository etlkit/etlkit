using System.Dynamic;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Scripting;
using EtlKit.AI;
using EtlKit.Rest;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests that ILogger&lt;T&gt; constructors work correctly on specialized (extension package) data flow classes.
/// </summary>
public class SpecializedLoggerInjectionTests
{
    [Fact]
    public void AIBatchTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<AIBatchTransformation>();

        var component = new AIBatchTransformation(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void JsonTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<JsonTransformation>();

        var component = new JsonTransformation(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void KafkaJsonSource_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<KafkaJsonSource<ExpandoObject>>();

        var component = new KafkaJsonSource<ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void KafkaStringTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<KafkaStringTransformation<ExpandoObject>>();

        var component = new KafkaStringTransformation<ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void KafkaTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<KafkaTransformation>();

        var component = new KafkaTransformation(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void RabbitMqTransformation_NonGeneric_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<RabbitMqTransformation>();

        var component = new RabbitMqTransformation(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void RabbitMqTransformation_Generic_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<RabbitMqTransformation<ExpandoObject, ExpandoObject>>();

        var component = new RabbitMqTransformation<ExpandoObject, ExpandoObject>(null, logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void RestTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<RestTransformation>();

        var component = new RestTransformation(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void ScriptedTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<ScriptedTransformation>();

        var component = new ScriptedTransformation(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void ScriptedRowTransformation_Generic_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<ScriptedRowTransformation<ExpandoObject, ExpandoObject>>();

        var component = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void XmlSource_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<XmlSource<ExpandoObject>>();

        var source = new XmlSource<ExpandoObject>(logger);

        Assert.Same(logger, source.Logger);
    }

    [Fact]
    public void XmlDestination_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<XmlDestination<ExpandoObject>>();

        var dest = new XmlDestination<ExpandoObject>(logger);

        Assert.Same(logger, dest.Logger);
    }

    [Fact]
    public void ExcelSource_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<ExcelSource<ExpandoObject>>();

        var source = new ExcelSource<ExpandoObject>(logger);

        Assert.Same(logger, source.Logger);
    }

    [Fact]
    public void CsvDestination_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<CsvDestination<ExpandoObject>>();

        var dest = new CsvDestination<ExpandoObject>(logger);

        Assert.Same(logger, dest.Logger);
    }

    [Fact]
    public void JsonDestination_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<JsonDestination<ExpandoObject>>();

        var dest = new JsonDestination<ExpandoObject>(logger);

        Assert.Same(logger, dest.Logger);
    }

    [Fact]
    public void CrossJoin_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<CrossJoin<ExpandoObject, ExpandoObject, ExpandoObject>>();

        var component = new CrossJoin<ExpandoObject, ExpandoObject, ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void RowDuplication_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<RowDuplication<ExpandoObject>>();

        var component = new RowDuplication<ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void RowMultiplication_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<RowMultiplication<ExpandoObject, ExpandoObject>>();

        var component = new RowMultiplication<ExpandoObject, ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void MergeJoin_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<MergeJoin<ExpandoObject, ExpandoObject, ExpandoObject>>();

        var component = new MergeJoin<ExpandoObject, ExpandoObject, ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void LookupTransformation_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<LookupTransformation<ExpandoObject, ExpandoObject>>();

        var component = new LookupTransformation<ExpandoObject, ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void DbMerge_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<DbMerge<ExpandoObject>>();

        var component = new DbMerge<ExpandoObject>(logger);

        Assert.Same(logger, component.Logger);
    }

    [Fact]
    public void CustomSource_WithLogger_ShouldUseInjectedLogger()
    {
        var (_, logger) = BuildLogger<CustomSource<ExpandoObject>>();

        var source = new CustomSource<ExpandoObject>(logger);

        Assert.Same(logger, source.Logger);
    }

    private static (ServiceProvider provider, ILogger<T> logger) BuildLogger<T>()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<T>>();
        return (provider, logger);
    }
}
