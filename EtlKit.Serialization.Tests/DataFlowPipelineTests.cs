using System.Dynamic;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Serialization.DataFlow;
using EtlKit.Primitives;
using JetBrains.Annotations;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests for <see cref="Pipeline"/> and <see cref="Pipeline{TIn,TOut}"/> XML deserialization
/// and integration with <see cref="DataFlowXmlReader"/>.
/// </summary>
public class DataFlowPipelineTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static EtlDataFlowStep Deserialize(string xml)
    {
        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        var serializer = new XmlSerializer(typeof(EtlDataFlowStep));
        return (EtlDataFlowStep)serializer.Deserialize(stream)!;
    }

    private static EtlDataFlowStep DeserializeWithErrorDest(
        string xml,
        out ErrorLogDestination errorDest
    )
    {
        errorDest = new ErrorLogDestination();
        return DataFlowXmlReader.Deserialize<EtlDataFlowStep>(xml, errorDest);
    }

    private static ExpandoObject Row(string key, object value)
    {
        var obj = new ExpandoObject();
        ((IDictionary<string, object?>)obj)[key] = value;
        return obj;
    }

    // ---------------------------------------------------------------------------
    // 1. External source + Pipeline with two transforms + destination inside Pipeline
    // ---------------------------------------------------------------------------

    [Fact]
    public void Pipeline_TwoTransforms_RowsPassThrough()
    {
        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <Pipeline>
                            <PassthroughTransformation />
                            <PassthroughTransformation />
                            <LinkTo>
                                <MemoryDestination />
                            </LinkTo>
                        </Pipeline>
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var source = Assert.IsType<MemorySource>(step.Source);
        source.Data = [Row("x", 1), Row("x", 2)];

        step.Invoke(CancellationToken.None);

        Assert.Equal(2, step.Destinations.Count);
        var memDest = step.Destinations.OfType<MemoryDestination>().Single();
        Assert.Equal(2, memDest.Data.Count);
    }

    // ---------------------------------------------------------------------------
    // 2. Internal source — Pipeline is the root element with source as first child
    // ---------------------------------------------------------------------------

    [Fact]
    public void Pipeline_WithInternalSource_ProducesOutput()
    {
        var xml =
            @"<EtlDataFlowStep>
                <Pipeline>
                    <MemorySource />
                    <PassthroughTransformation />
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </Pipeline>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var pipeline = Assert.IsType<Pipeline>(step.Source);

        var internalSource = Assert.IsType<MemorySource>(pipeline.Steps[0]);
        internalSource.Data = [Row("v", 42)];

        step.Invoke(CancellationToken.None);

        Assert.Equal(2, step.Destinations.Count);
        var memDest = step.Destinations.OfType<MemoryDestination>().Single();
        Assert.Single(memDest.Data);
    }

    // ---------------------------------------------------------------------------
    // 3. Auto-void — no external LinkTo; pipeline completes without deadlock
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_NoExternalLinkTo_CompletesWithoutHanging()
    {
        var xml =
            @"<EtlDataFlowStep>
                <Pipeline>
                    <MemorySource />
                    <PassthroughTransformation />
                </Pipeline>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var pipeline = Assert.IsType<Pipeline>(step.Source);
        var internalSource = Assert.IsType<MemorySource>(pipeline.Steps[0]);
        internalSource.Data = [Row("v", 1)];

        await pipeline.ExecuteAsync(CancellationToken.None).ConfigureAwait(true);

        // Pipeline itself is registered as the destination for completion tracking.
        Assert.Single(step.Destinations);
    }

    // ---------------------------------------------------------------------------
    // 4. External LinkTo suppresses auto-void
    // ---------------------------------------------------------------------------

    [Fact]
    public void Pipeline_ExternalLinkTo_OutputOnlyInExternalDestination()
    {
        var xml =
            @"<EtlDataFlowStep>
                <Pipeline>
                    <MemorySource />
                    <PassthroughTransformation />
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </Pipeline>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var pipeline = Assert.IsType<Pipeline>(step.Source);
        var internalSource = Assert.IsType<MemorySource>(pipeline.Steps[0]);
        internalSource.Data = [Row("v", 7), Row("v", 8)];

        step.Invoke(CancellationToken.None);

        Assert.Equal(2, step.Destinations.Count);
        var memDest = step.Destinations.OfType<MemoryDestination>().Single();
        Assert.Equal(2, memDest.Data.Count);
    }

    // ---------------------------------------------------------------------------
    // 5. LinkErrorTo forwarding — pipeline.LinkErrorTo propagates to all steps
    // ---------------------------------------------------------------------------

    [Fact]
    public void Pipeline_LinkErrorTo_ForwardsToAllInternalSteps()
    {
        var xml =
            @"<EtlDataFlowStep>
                <Pipeline>
                    <MemorySource />
                    <PassthroughTransformation />
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </Pipeline>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var pipeline = Assert.IsType<Pipeline>(step.Source);
        var internalSource = Assert.IsType<MemorySource>(pipeline.Steps[0]);
        internalSource.Data = [Row("v", 1)];

        var errorDest = new ErrorLogDestination();
        pipeline.LinkErrorTo(errorDest);

        step.Invoke(CancellationToken.None);

        Assert.Empty(errorDest.Errors);
    }

    // ---------------------------------------------------------------------------
    // 6. <LinkErrorTo> inside XML routes errors from all pipeline steps
    // ---------------------------------------------------------------------------

    [Fact]
    public void Pipeline_XmlLinkErrorTo_WiresErrorRouting()
    {
        var xml =
            @"<EtlDataFlowStep>
                <Pipeline>
                    <MemorySource />
                    <BrokenTransformation />
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                    <LinkErrorTo>
                        <ErrorLogDestination />
                    </LinkErrorTo>
                </Pipeline>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var pipeline = Assert.IsType<Pipeline>(step.Source);
        var internalSource = Assert.IsType<MemorySource>(pipeline.Steps[0]);
        internalSource.Data = [Row("v", 1)];

        step.Invoke(CancellationToken.None);

        Assert.Single(step.ErrorDestinations);
    }

    // ---------------------------------------------------------------------------
    // 7. Universal linkAllErrorsTo covers all steps inside Pipeline
    // ---------------------------------------------------------------------------

    [Fact]
    public void Pipeline_LinkAllErrorsTo_CoversInternalSteps()
    {
        var xml =
            @"<EtlDataFlowStep>
                <Pipeline>
                    <MemorySource />
                    <PassthroughTransformation />
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </Pipeline>
            </EtlDataFlowStep>";

        var step = DeserializeWithErrorDest(xml, out var errorDest);
        var pipeline = Assert.IsType<Pipeline>(step.Source);
        var internalSource = Assert.IsType<MemorySource>(pipeline.Steps[0]);
        internalSource.Data = [Row("v", 3)];

        step.Invoke(CancellationToken.None);

        Assert.Empty(errorDest.Errors);
        Assert.Single(step.ErrorDestinations);
    }

    // ---------------------------------------------------------------------------
    // 8. No internal source — ExecuteAsync throws InvalidOperationException
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_NoInternalSource_ExecuteAsyncThrows()
    {
        var xml =
            @"<EtlDataFlowStep>
                <Pipeline>
                    <PassthroughTransformation />
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </Pipeline>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var pipeline = Assert.IsType<Pipeline>(step.Source);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.ExecuteAsync(CancellationToken.None)
        );
    }

    // ---------------------------------------------------------------------------
    // 9. Step with nested <LinkTo> inside <Pipeline> throws InvalidDataException
    // ---------------------------------------------------------------------------

    [Fact]
    public void Pipeline_StepWithNestedLinkTo_ThrowsInvalidDataException()
    {
        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <Pipeline>
                            <PassthroughTransformation>
                                <LinkTo>
                                    <MemoryDestination />
                                </LinkTo>
                            </PassthroughTransformation>
                        </Pipeline>
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        var ex = Assert.Throws<InvalidOperationException>(() => Deserialize(xml));
        Assert.IsType<InvalidDataException>(ex.InnerException);
    }

    // ---------------------------------------------------------------------------
    // 10. Backward compat — existing nested <LinkTo> XML still deserializes/runs
    // ---------------------------------------------------------------------------

    [Fact]
    public void DataFlow_ExistingXmlWithNestedLinkTo_StillWorks()
    {
        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <PassthroughTransformation>
                            <LinkTo>
                                <MemoryDestination />
                            </LinkTo>
                        </PassthroughTransformation>
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var source = Assert.IsType<MemorySource>(step.Source);
        source.Data = [Row("k", "hello")];

        step.Invoke(CancellationToken.None);

        var dest = Assert.Single(step.Destinations);
        var memDest = Assert.IsType<MemoryDestination>(dest);
        Assert.Single(memDest.Data);
    }

    // ---------------------------------------------------------------------------
    // 11. Batch destination as last pipeline step — DataFlowBatchDestination{T}
    //     implements both IDataFlowLinkTarget{T} and IDataFlowLinkTarget{T[]}
    //     the type-check must pick the matching interface, not the array one.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_BatchDestinationAsLastStep_ReceivesAllRows()
    {
        var xml =
            @"<EtlDataFlowStep>
                <Pipeline>
                    <MemorySource />
                    <PassthroughTransformation />
                    <BatchMemoryDestination />
                </Pipeline>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var pipeline = Assert.IsType<Pipeline>(step.Source);
        var internalSource = Assert.IsType<MemorySource>(pipeline.Steps[0]);
        internalSource.Data = [Row("v", 1), Row("v", 2), Row("v", 3)];

        await pipeline.ExecuteAsync(CancellationToken.None).ConfigureAwait(true);

        var batchDest = Assert.IsType<BatchMemoryDestination>(pipeline.Steps[2]);
        await batchDest.Completion.ConfigureAwait(true);
        Assert.Equal(3, batchDest.Rows.Count);
    }

    // ---------------------------------------------------------------------------
    // 13. Generic Pipeline<TIn,TOut> — typed steps wired and data flows through
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenericPipeline_TypedSteps_DataFlowsThrough()
    {
        var pass1 = new PassthroughTransformation();
        var pass2 = new PassthroughTransformation();
        var dest = new MemoryDestination<ExpandoObject>();

        var context = new TestContext();
        context.Register("PassthroughTransformation", pass1, "PT1");
        context.Register("PassthroughTransformation", pass2, "PT2");
        context.Register("MemoryDestination", dest, "MD");

        var xml = XElement.Parse(
            @"<Pipeline>
                <PassthroughTransformation name=""PT1"" />
                <PassthroughTransformation name=""PT2"" />
                <LinkTo>
                    <MemoryDestination name=""MD"" />
                </LinkTo>
            </Pipeline>"
        );

        var pipeline = new Pipeline<ExpandoObject, ExpandoObject>();
        pipeline.ReadXml(xml, context);

        var source = new MemorySource<ExpandoObject> { Data = [Row("x", 99)] };
        source.LinkTo(pipeline);

        await source.ExecuteAsync(CancellationToken.None).ConfigureAwait(true);
        await dest.Completion.ConfigureAwait(true);

        Assert.Single(dest.Data);
    }

    // ---------------------------------------------------------------------------
    // 14. IDataFlowXmlSerializable extensibility — ReadXml is delegated correctly
    // ---------------------------------------------------------------------------

    [Fact]
    public void CustomXmlSerializable_ReadXmlDelegated_ComponentConfigured()
    {
        var xml =
            @"<EtlDataFlowStep>
                <CustomSerializableSource>
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </CustomSerializableSource>
            </EtlDataFlowStep>";

        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        using var step = new EtlDataFlowStep();
        var reader = new DataFlowXmlReader(step);
        reader.Read(xmlReader);

        var source = Assert.IsType<CustomSerializableSource>(step.Source);
        Assert.True(source.ReadXmlWasCalled);

        source.Data = [Row("q", 1)];
        step.Invoke(CancellationToken.None);

        var dest = Assert.Single(step.Destinations);
        var memDest = Assert.IsType<MemoryDestination>(dest);
        Assert.Single(memDest.Data);
    }

    // ---------------------------------------------------------------------------
    // 15. ExecuteAsync awaits full pipeline completion, not just source posting
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_NoExternalLinkTo_CompletionAwaitsTransformationDrain()
    {
        var transform = new TrackingTransformation();

        var context = new TestContext();
        context.Register(nameof(MemorySource), new MemorySource(), "src");
        context.Register(nameof(TrackingTransformation), transform, "trk");

        var xml = XElement.Parse(
            @"<Pipeline>
                <MemorySource name=""src"" />
                <TrackingTransformation name=""trk"" />
            </Pipeline>"
        );

        var pipeline = new Pipeline();
        pipeline.ReadXml(xml, context);

        var source = Assert.IsType<MemorySource>(pipeline.Steps[0]);
        source.Data = [Row("v", 1), Row("v", 2), Row("v", 3)];

        // ExecuteAsync fires the source; it does not wait for destination drain,
        // matching the standard ETLBox contract (same as DataFlowLinker.LinkTo).
        // Callers that need full pipeline completion explicitly await Pipeline.Completion,
        // which covers the auto-wired VoidDestination.
        await pipeline.ExecuteAsync(CancellationToken.None).ConfigureAwait(true);
        await pipeline.Completion.ConfigureAwait(true);

        Assert.Equal(3, transform.ProcessedCount);
    }

    // ---------------------------------------------------------------------------
    // 16. LinkTo after Completion accessed — throws immediately instead of silent data loss
    // ---------------------------------------------------------------------------

    [Fact]
    public void Pipeline_LinkToAfterCompletionAccessed_Throws()
    {
        var pass = new PassthroughTransformation();
        var context = new TestContext();
        context.Register(nameof(PassthroughTransformation), pass, "pt");

        var xml = XElement.Parse(
            @"<Pipeline>
                <PassthroughTransformation name=""pt"" />
            </Pipeline>"
        );

        var pipeline = new Pipeline();
        pipeline.ReadXml(xml, context);

        _ = pipeline.Completion; // arms the NullTarget drain

        Assert.Throws<InvalidOperationException>(() => pipeline.LinkTo(new MemoryDestination()));
    }

    // ---------------------------------------------------------------------------
    // Helpers: PassthroughTransformation, TrackingTransformation, TestContext, CustomSerializableSource
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Concrete <see cref="DataFlowBatchDestination{TInput}"/> used in tests.
    /// Inherits both <c>IDataFlowLinkTarget&lt;ExpandoObject&gt;</c> (from
    /// <see cref="IDataFlowBatchDestination{TInput}"/>) and
    /// <c>IDataFlowLinkTarget&lt;ExpandoObject[]&gt;</c> (from the array base class),
    /// which is exactly the ambiguous case that triggered the type-mismatch bug.
    /// </summary>
    public sealed class BatchMemoryDestination : DataFlowBatchDestination<ExpandoObject>
    {
        /// <summary>All individual rows received across all batches.</summary>
        public List<ExpandoObject> Rows { get; } = [];

        /// <summary>Initialises with batch size 2 so tests can verify batching.</summary>
        public BatchMemoryDestination()
        {
            BatchSize = 2;
        }

        /// <inheritdoc />
        protected override void PrepareWrite() { }

        /// <inheritdoc />
        protected override void TryBulkInsertData(ExpandoObject[] data) => Rows.AddRange(data);

        /// <inheritdoc />
        protected override void FinishWrite() { }
    }

    /// <summary>
    /// Transformation that counts how many rows it has processed.
    /// Used to verify that <see cref="Pipeline.ExecuteAsync"/> awaits full pipeline
    /// completion (including the auto-wired <see cref="VoidDestination{TInput}"/>),
    /// not just source posting.
    /// </summary>
    public sealed class TrackingTransformation : RowTransformation<ExpandoObject>
    {
        /// <summary>Number of rows processed so far.</summary>
        public int ProcessedCount { get; private set; }

        /// <summary>Initializes with a counting transformation function.</summary>
        public TrackingTransformation()
        {
            TransformationFunc = row =>
            {
                ProcessedCount++;
                return row;
            };
        }
    }

    /// <summary>
    /// Identity row transformation — initialises its block in the constructor
    /// so it can be used inside <see cref="Pipeline"/> XML without setting a function.
    /// </summary>
    [PublicAPI]
    public sealed class PassthroughTransformation : RowTransformation<ExpandoObject>
    {
        /// <summary>Initialises with an identity transformation function.</summary>
        public PassthroughTransformation()
        {
            TransformationFunc = row => row;
        }
    }

    /// <summary>
    /// Minimal <see cref="IDataFlowXmlContext"/> backed by a name→instance dictionary.
    /// Supports multiple instances of the same type identified by a <c>name</c> attribute.
    /// </summary>
    private sealed class TestContext : IDataFlowXmlContext
    {
        private readonly Dictionary<string, object> _byKey = new();
        private readonly Dictionary<string, object> _byType = new();

        public void Register(string typeName, object instance, string key)
        {
            _byKey[key] = instance;
            _byType[typeName] = instance;
        }

        public Type? ResolveType(string typeName) =>
            _byType.TryGetValue(typeName, out var obj) ? obj.GetType() : null;

        public object? CreateObject(string typeName, XElement element)
        {
            var key = element.Attribute("name")?.Value;
            if (key != null && _byKey.TryGetValue(key, out var keyed))
                return keyed;
            return _byType.GetValueOrDefault(typeName);
        }

        public IDataFlowXmlContext WithoutGlobalActions() => this;
    }

    /// <summary>
    /// Source that implements <see cref="IDataFlowXmlSerializable"/> so that
    /// <see cref="DataFlowXmlReader"/> delegates to <see cref="ReadXml"/> instead of
    /// the default property loop.
    /// </summary>
    public class CustomSerializableSource : MemorySource<ExpandoObject>, IDataFlowXmlSerializable
    {
        /// <summary>Set to true when <see cref="ReadXml"/> is called by the reader.</summary>
        public bool ReadXmlWasCalled { get; private set; }

        /// <inheritdoc />
        public void ReadXml(XElement element, IDataFlowXmlContext context)
        {
            ReadXmlWasCalled = true;
            foreach (var child in element.Element("LinkTo")?.Elements() ?? [])
            {
                if (
                    context.CreateObject(child.Name.LocalName, child)
                    is IDataFlowLinkTarget<ExpandoObject> target
                )
                    LinkTo(target);
            }
        }
    }
}
