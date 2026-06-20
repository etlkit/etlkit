using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using EtlKit.Serialization.DataFlow;
using EtlKit.Primitives;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AccessToDisposedClosure
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests for IDataFlow disposable resource ownership: GetOrAddResource and Dispose behavior,
/// plus DataFlowXmlReader integration that auto-registers IDisposable properties.
/// </summary>
public sealed class DataFlowDisposableResourceTests
{
    // ── Pure EtlDataFlowStep unit tests ─────────────────────────────────────

    [Fact]
    public void GetOrAddResource_SameKey_ReturnsSameInstance()
    {
        using var step = new EtlDataFlowStep();

        var first = step.GetOrAddResource("key1", () => new TrackableResource { Config = "a" });
        var second = step.GetOrAddResource("key1", () => new TrackableResource { Config = "b" });

        Assert.Same(first, second);
    }

    [Fact]
    public void GetOrAddResource_DifferentKeys_ReturnDifferentInstances()
    {
        using var step = new EtlDataFlowStep();

        var first = step.GetOrAddResource("key1", () => new TrackableResource { Config = "a" });
        var second = step.GetOrAddResource("key2", () => new TrackableResource { Config = "a" });

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetOrAddResource_Generic_ReturnTypedInstance()
    {
        using var step = new EtlDataFlowStep();

        var result = step.GetOrAddResource("key1", () => new TrackableResource { Config = "x" });

        Assert.IsType<TrackableResource>(result);
        Assert.Equal("x", ((TrackableResource)result).Config);
    }

    [Fact]
    public void GetOrAddResource_GenericExtension_ReturnTypedInstance()
    {
        using var step = new EtlDataFlowStep();

        var result = DataFlowResourceOwnerExtensions.GetOrAddResource(
            step,
            "key1",
            () => new TrackableResource { Config = "y" }
        );

        Assert.IsType<TrackableResource>(result);
        Assert.Equal("y", result.Config);
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public void Dispose_DisposesRegisteredResource()
    {
        var resource = new TrackableResource();
        var step = new EtlDataFlowStep();
        step.GetOrAddResource("r1", () => resource);

        step.Dispose();

        Assert.True(resource.IsDisposed);
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public void Dispose_MultipleResources_AllDisposed()
    {
        var r1 = new TrackableResource();
        var r2 = new TrackableResource();
        var step = new EtlDataFlowStep();
        step.GetOrAddResource("r1", () => r1);
        step.GetOrAddResource("r2", () => r2);

        step.Dispose();

        Assert.True(r1.IsDisposed);
        Assert.True(r2.IsDisposed);
    }

    [Fact]
    public void ResourceCount_ReflectsAddedResources()
    {
        using var step = new EtlDataFlowStep();
        step.GetOrAddResource("r1", () => new TrackableResource());
        step.GetOrAddResource("r2", () => new TrackableResource());

        Assert.Equal(2, step.ResourceCount());
    }

    [Fact]
    public void ResourceCount_SameKeyNotDuplicated()
    {
        using var step = new EtlDataFlowStep();
        step.GetOrAddResource("r1", () => new TrackableResource());
        step.GetOrAddResource("r1", () => new TrackableResource());

        Assert.Equal(1, step.ResourceCount());
    }

    // ── XML integration: SetClassProperty path (concrete IDisposable) ────────

    [Fact]
    public void XmlReader_ConcreteDisposableStepProperty_RegisteredWithFlow()
    {
        var xml =
            @"<StepWithConcreteResource>
                <Resource>
                    <Config>hello</Config>
                </Resource>
            </StepWithConcreteResource>";

        var step = DeserializeStep<StepWithConcreteResource>(xml);

        Assert.Equal(1, step.ResourceCount());
        Assert.NotNull(step.Resource);
        Assert.Equal("hello", step.Resource!.Config);
    }

    [Fact]
    public void XmlReader_SameConcreteDisposableConfig_SharedInstance()
    {
        // Two elements with identical XML → same resource instance (dedup by content key)
        var xml =
            @"<StepWithTwoResources>
                <First>
                    <Config>shared</Config>
                </First>
                <Second>
                    <Config>shared</Config>
                </Second>
            </StepWithTwoResources>";

        var step = DeserializeStep<StepWithTwoResources>(xml);

        Assert.Equal(1, step.ResourceCount());
        Assert.Same(step.First, step.Second);
    }

    [Fact]
    public void XmlReader_DifferentConcreteDisposableConfig_SeparateInstances()
    {
        var xml =
            @"<StepWithTwoResources>
                <First>
                    <Config>config-a</Config>
                </First>
                <Second>
                    <Config>config-b</Config>
                </Second>
            </StepWithTwoResources>";

        var step = DeserializeStep<StepWithTwoResources>(xml);

        Assert.Equal(2, step.ResourceCount());
        Assert.NotSame(step.First, step.Second);
    }

    [Fact]
    public void XmlReader_ConcreteDisposableStepProperty_DisposedWithFlow()
    {
        var xml =
            @"<StepWithConcreteResource>
                <Resource>
                    <Config>test</Config>
                </Resource>
            </StepWithConcreteResource>";

        var step = DeserializeStep<StepWithConcreteResource>(xml);
        var resource = step.Resource!;

        step.Dispose();

        Assert.True(resource.IsDisposed);
    }

    // ── XML integration: SetInterfaceProperty path (abstract IDisposable) ────

    [Fact]
    public void XmlReader_AbstractDisposableNestedProperty_RegisteredWithFlow()
    {
        var xml =
            @"<StepWithHostComponent>
                <Component>
                    <Resource type=""ConcreteResource"">
                        <Config>hello</Config>
                    </Resource>
                </Component>
            </StepWithHostComponent>";

        var step = DeserializeStep<StepWithHostComponent>(xml);

        Assert.Equal(1, step.ResourceCount());
        Assert.NotNull(step.Component?.Resource);
        Assert.Equal("hello", step.Component!.Resource!.Config);
    }

    [Fact]
    public void XmlReader_SameAbstractDisposableConfig_SharedInstance()
    {
        var xml =
            @"<StepWithTwoHostComponents>
                <First>
                    <Resource type=""ConcreteResource"">
                        <Config>shared</Config>
                    </Resource>
                </First>
                <Second>
                    <Resource type=""ConcreteResource"">
                        <Config>shared</Config>
                    </Resource>
                </Second>
            </StepWithTwoHostComponents>";

        var step = DeserializeStep<StepWithTwoHostComponents>(xml);

        Assert.Equal(1, step.ResourceCount());
        Assert.Same(step.First!.Resource, step.Second!.Resource);
    }

    [Fact]
    public void XmlReader_AbstractDisposableNestedProperty_DisposedWithFlow()
    {
        var xml =
            @"<StepWithHostComponent>
                <Component>
                    <Resource type=""ConcreteResource"">
                        <Config>test</Config>
                    </Resource>
                </Component>
            </StepWithHostComponent>";

        var step = DeserializeStep<StepWithHostComponent>(xml);
        var resource = (ConcreteResource)step.Component!.Resource!;

        step.Dispose();

        Assert.True(resource.IsDisposed);
    }

    // ── Regression: legacy IDataFlow without IDataFlowResourceOwner (RSSL-11719) ──

    [Fact]
    public void XmlReader_LegacyDataFlowWithoutResourceOwner_FallsBackWithoutThrowing()
    {
        // RSSL-11719: an external IDataFlow implementation compiled against an earlier ETLBox version
        // does NOT implement IDataFlowResourceOwner. The reader must gracefully fall back to plain
        // creation (no dedup/ownership) instead of invoking the missing capability and throwing.
        var xml =
            @"<LegacyStepWithResource>
                <Resource>
                    <Config>legacy</Config>
                </Resource>
            </LegacyStepWithResource>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        using var step = new LegacyStepWithResource();
        var reader = new DataFlowXmlReader(step);

        var exception = Record.Exception(() => reader.Read(xmlReader));

        Assert.Null(exception);
        Assert.NotNull(step.Resource);
        Assert.Equal("legacy", step.Resource!.Config);
    }

    // ── Activator-based lifetime ownership (RSSL-11719, part b) ──────────────

    [Fact]
    public void XmlReader_DiOwnedDisposable_NotRegisteredOrDisposedByFlow()
    {
        // A disposable resolved from a DI container is owned by the container, not the data flow:
        // the flow must neither register it for disposal nor dispose it.
        using var externalResource = new TrackableResource();
        var services = new ServiceCollection();
        services.AddSingleton(externalResource);
        using var provider = services.BuildServiceProvider();

        var xml =
            @"<StepWithConcreteResource>
                <Resource>
                    <Config>external</Config>
                </Resource>
            </StepWithConcreteResource>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        var step = new StepWithConcreteResource();
        var reader = new DataFlowXmlReader(step, new ServiceProviderActivator(provider));
        reader.Read(xmlReader);

        Assert.Same(externalResource, step.Resource);
        Assert.Equal(0, step.ResourceCount());

        step.Dispose();

        Assert.False(externalResource.IsDisposed);
    }

    [Fact]
    public void XmlReader_UnregisteredDisposableViaServiceProvider_FlowOwnedAndDisposed()
    {
        // A disposable NOT registered in the container is created fresh (ActivatorUtilities) and is
        // owned by the data flow — registered for disposal and disposed with the flow.
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var xml =
            @"<StepWithConcreteResource>
                <Resource>
                    <Config>new</Config>
                </Resource>
            </StepWithConcreteResource>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        var step = new StepWithConcreteResource();
        var reader = new DataFlowXmlReader(step, new ServiceProviderActivator(provider));
        reader.Read(xmlReader);

        Assert.Equal(1, step.ResourceCount());
        var resource = step.Resource!;

        step.Dispose();

        Assert.True(resource.IsDisposed);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static T DeserializeStep<T>(string xml)
        where T : EtlDataFlowStep, new()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        var step = new T();
        var reader = new DataFlowXmlReader(step);
        reader.Read(xmlReader);
        return step;
    }

    // ── test fixtures ────────────────────────────────────────────────────────

    public sealed class TrackableResource : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public string? Config { get; set; }

        public void Dispose() => IsDisposed = true;
    }

    public abstract class AbstractResource : IDisposable
    {
        public string? Config { get; set; }
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                IsDisposed = true;
        }
    }

    public sealed class ConcreteResource : AbstractResource { }

    public sealed class ComponentWithAbstractResource
    {
        public AbstractResource? Resource { get; set; }
    }

    public sealed class StepWithConcreteResource : EtlDataFlowStep
    {
        public TrackableResource? Resource { get; set; }
    }

    public sealed class StepWithTwoResources : EtlDataFlowStep
    {
        public TrackableResource? First { get; set; }
        public TrackableResource? Second { get; set; }
    }

    public sealed class StepWithHostComponent : EtlDataFlowStep
    {
        public ComponentWithAbstractResource? Component { get; set; }
    }

    public sealed class StepWithTwoHostComponents : EtlDataFlowStep
    {
        public ComponentWithAbstractResource? First { get; set; }
        public ComponentWithAbstractResource? Second { get; set; }
    }

    // Legacy IDataFlow implementation WITHOUT IDataFlowResourceOwner — simulates an external
    // implementer (e.g. RapidSoft.Etl EtlDataFlowStep) compiled against ETLBox < 1.19 (RSSL-11719).
    [Serializable]
    public class LegacyDataFlowStep : IDataFlow, IXmlSerializable
    {
        private readonly DataFlowResources _resources = new();

        public IConnectionManager GetOrAddConnectionManager(
            Type connectionManagerType,
            string? key,
            Func<Type, string?, IConnectionManager> factory
        ) => _resources.GetOrAddConnectionManager(connectionManagerType, key, factory);

        public IDataFlowSource<ExpandoObject> Source { get; set; } = null!;
        public IList<IDataFlowDestination<ExpandoObject>> Destinations { get; set; } = null!;
        public IList<IDataFlowDestination<EtlKitError>> ErrorDestinations { get; set; } = null!;

        public XmlSchema? GetSchema() => null;

        public void ReadXml(XmlReader reader) => new DataFlowXmlReader(this).Read(reader);

        public void WriteXml(XmlWriter writer) => throw new NotSupportedException();

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                _resources.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public sealed class LegacyStepWithResource : LegacyDataFlowStep
    {
        public TrackableResource? Resource { get; set; }
    }
}
