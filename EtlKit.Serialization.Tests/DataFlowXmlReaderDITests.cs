using System.Dynamic;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using EtlKit.DataFlow;
using EtlKit.Extensions;
using EtlKit.Serialization.DataFlow;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests for DataFlowXmlReader DI support (IServiceProvider, IDataFlowActivator).
/// </summary>
public class DataFlowXmlReaderDITests
{
    [Fact]
    public void Constructor_WithServiceProvider_ShouldUseServiceProviderActivator()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        using var dataFlow = new EtlDataFlowStep();

        var reader = new DataFlowXmlReader(dataFlow, serviceProvider: provider);

        Assert.NotNull(reader);
    }

    [Fact]
    public void Constructor_WithoutServiceProvider_ShouldUseDefaultActivator()
    {
        using var dataFlow = new EtlDataFlowStep();

        var reader = new DataFlowXmlReader(dataFlow);

        Assert.NotNull(reader);
    }

    [Fact]
    public void Constructor_WithCustomActivator_ShouldUseProvidedActivator()
    {
        using var dataFlow = new EtlDataFlowStep();
        var activator = new DefaultDataFlowActivator();

        var reader = new DataFlowXmlReader(dataFlow, activator);

        Assert.NotNull(reader);
    }

    [Fact]
    public void Constructor_WithNullActivator_ShouldThrowArgumentNullException()
    {
        using var dataFlow = new EtlDataFlowStep();

        var act = () => new DataFlowXmlReader(dataFlow, (IDataFlowActivator)null!);

        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("activator", ex.ParamName);
    }

    [Fact]
    public void Read_WithServiceProvider_ShouldDeserializeComponents()
    {
        // Arrange
        var xml =
            @"<EtlDataFlowStepWithDI>
                <MemorySource>
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStepWithDI>";

        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        var serializer = new XmlSerializer(typeof(EtlDataFlowStepWithDI));
        var step = (EtlDataFlowStepWithDI)serializer.Deserialize(stream)!;

        // Assert
        Assert.NotNull(step);
        Assert.NotNull(step.Source);
        Assert.IsAssignableFrom<MemorySource<ExpandoObject>>(step.Source);
        Assert.Single(step.Destinations);
    }

    [Fact]
    public void Read_WithCustomActivator_ShouldUseActivator()
    {
        // Arrange - use a tracking activator to verify it was called
        var trackingActivator = new TrackingActivator();

        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        using var dataFlow = new EtlDataFlowStep();
        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        var reader = new DataFlowXmlReader(dataFlow, trackingActivator);
        reader.Read(xmlReader);

        // Assert
        Assert.NotEmpty(trackingActivator.CreatedTypes);
    }

    [Fact]
    public void Deserialize_WithServiceProvider_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var errorDest = new ErrorLogDestination();

        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        // Act
        var step = DataFlowXmlReader.Deserialize<EtlDataFlowStep>(xml, errorDest, provider);

        // Assert
        Assert.NotNull(step);
        Assert.NotNull(step.Source);
    }

    [Fact]
    public void Deserialize_WithNullServiceProvider_ShouldUseDefaultActivator()
    {
        // Arrange
        var errorDest = new ErrorLogDestination();

        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        // Act
        var step = DataFlowXmlReader.Deserialize<EtlDataFlowStep>(xml, errorDest, null);

        // Assert
        Assert.NotNull(step);
        Assert.NotNull(step.Source);
    }

    [Fact]
    public void Deserialize_WithServiceProvider_CsvSourceWithConfiguration_ShouldDeserializeCorrectly()
    {
        // Arrange — CsvConfiguration has no parameterless constructor (requires CultureInfo).
        // AddEtlBoxCore registers a factory for CsvConfiguration so that
        // ServiceProviderActivator can resolve it during XML deserialization.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlBoxCore(CultureInfo.InvariantCulture);
        var provider = services.BuildServiceProvider();
        var errorDest = new ErrorLogDestination();

        // language=xml
        var xml =
            @"<EtlDataFlowStep>
                <CsvSource>
                    <Configuration>
                        <Delimiter>;</Delimiter>
                        <Escape>#</Escape>
                        <Quote>$</Quote>
                    </Configuration>
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </CsvSource>
            </EtlDataFlowStep>";

        // Act
        var step = DataFlowXmlReader.Deserialize<EtlDataFlowStep>(xml, errorDest, provider);

        // Assert
        Assert.NotNull(step);
        Assert.NotNull(step.Source);
        var csvSource = Assert.IsAssignableFrom<CsvSource<ExpandoObject>>(step.Source);
        Assert.NotNull(csvSource.Configuration);
        Assert.Equal(";", csvSource.Configuration.Delimiter);
        Assert.Equal('#', csvSource.Configuration.Escape);
        Assert.Equal('$', csvSource.Configuration.Quote);
    }

    /// <summary>
    /// Custom EtlDataFlowStep that uses IServiceProvider for DI.
    /// </summary>
    [Serializable]
    public class EtlDataFlowStepWithDI : EtlDataFlowStep
    {
        public override void ReadXml(XmlReader reader)
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var xmlReader = new DataFlowXmlReader(this, serviceProvider: provider);
            xmlReader.Read(reader);
        }
    }

    /// <summary>
    /// Activator that tracks which types it creates, delegating to DefaultDataFlowActivator.
    /// </summary>
    private class TrackingActivator : IDataFlowActivator
    {
        private readonly DefaultDataFlowActivator _inner = new();
        public List<Type> CreatedTypes { get; } = [];

        public object? CreateInstance(Type type)
        {
            CreatedTypes.Add(type);
            return _inner.CreateInstance(type);
        }
    }
}
