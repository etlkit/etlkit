using System.Dynamic;
using System.Text;
using System.Xml.Serialization;
using EtlKit.Common.DataFlow;
using EtlKit.Common.DataFlow.Streaming;
using EtlKit.DataFlow;
using JetBrains.Annotations;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// End-to-end XML deserialization tests for <see cref="CheckpointWriter"/>: the whole pipeline —
/// source, writer, and checkpoint store — is declared in XML, executed, and the committed
/// position is asserted through the store. Confirms the checkpoint model is usable from
/// XML-defined ETL packages, not only from code-built flows.
/// </summary>
public class CheckpointWriterDeserializationTests
{
    // Non-generic on purpose: DataFlowXmlReader resolves interface properties by simple type
    // name via the "type" attribute, which cannot close open generics — same reason the
    // production wrapper under test exists.
    [UsedImplicitly]
    public sealed class InMemoryLongCheckpointStore : ICheckpointStore<long>
    {
        private readonly Dictionary<string, long> _positions = [];

        public int CommitCount { get; private set; }

        public Task<(bool Found, long Position)> LoadAsync(
            string checkpointId,
            CancellationToken ct
        ) =>
            Task.FromResult(
                _positions.TryGetValue(checkpointId, out var p) ? (true, p) : (false, 0L)
            );

        public Task CommitAsync(string checkpointId, long position, CancellationToken ct)
        {
            _positions[checkpointId] = position;
            CommitCount++;
            return Task.CompletedTask;
        }
    }

    private static EtlDataFlowStep Deserialize(string xml)
    {
        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        var serializer = new XmlSerializer(typeof(EtlDataFlowStep));
        return (EtlDataFlowStep)serializer.Deserialize(stream)!;
    }

    private static ExpandoObject Row(long id, string name)
    {
        var row = new ExpandoObject();
        var dict = (IDictionary<string, object?>)row;
        dict["Id"] = id;
        dict["Name"] = name;
        return row;
    }

    [Fact]
    public async Task CheckpointWriter_FullXmlPipeline_CommitsMaxPositionFromColumn()
    {
        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <CheckpointWriter>
                            <CheckpointId>xml-e2e-checkpoint</CheckpointId>
                            <PositionColumn>Id</PositionColumn>
                            <CheckpointStore type=""InMemoryLongCheckpointStore"" />
                        </CheckpointWriter>
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        var step = Deserialize(xml);
        var source = Assert.IsType<MemorySource>(step.Source);
        // Out of arrival order on purpose: the committed position must be the maximum seen,
        // not the last seen.
        source.Data = [Row(2, "second"), Row(3, "third"), Row(1, "first")];

        step.Invoke(CancellationToken.None);

        var writer = Assert.IsType<CheckpointWriter>(Assert.Single(step.Destinations));
        var store = Assert.IsType<InMemoryLongCheckpointStore>(writer.CheckpointStore);
        Assert.True(store.CommitCount > 0);
        var (found, position) = await store.LoadAsync("xml-e2e-checkpoint", CancellationToken.None);
        Assert.True(found);
        Assert.Equal(3, position);
    }
}
