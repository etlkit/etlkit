using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;

using EtlKit.Common.DataFlow;
using EtlKit.Common.DataFlow.Streaming;

using JetBrains.Annotations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EtlKit.DataFlow;

/// <summary>
/// Consumes a MongoDB Change Stream and emits change events into the data flow pipeline.
/// Requires the MongoDB deployment to be in replica set mode (single-node replica set is sufficient).
/// </summary>
/// <remarks>
/// Uses <c>IMongoCollection.Watch()</c> with a resume token stored in <see cref="CheckpointStore"/>
/// so that processing can safely restart from the last committed position.
/// </remarks>
[PublicAPI]
public class MongoChangeStreamSource<TOutput> : DataFlowSource<TOutput>
{
    /// <summary>MongoDB client used to access the database and collection.</summary>
    public IMongoClient MongoClient { get; set; } = null!;

    /// <summary>Name of the MongoDB database.</summary>
    public string Database { get; set; } = null!;

    /// <summary>Name of the collection to watch.</summary>
    public string Collection { get; set; } = null!;

    /// <summary>
    /// Optional aggregation pipeline to filter or transform change stream documents.
    /// When <c>null</c>, all changes to the collection are emitted.
    /// </summary>
    public PipelineDefinition<
        ChangeStreamDocument<BsonDocument>,
        ChangeStreamDocument<BsonDocument>
    >? Pipeline { get; set; }

    /// <summary>Maximum time the server waits for new events before returning an empty batch. Defaults to 1 second.</summary>
    public TimeSpan MaxAwaitTime { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Controls which version of the full document is returned on updates. Defaults to <c>UpdateLookup</c>.</summary>
    public ChangeStreamFullDocumentOption FullDocument { get; set; } =
        ChangeStreamFullDocumentOption.UpdateLookup;

    /// <summary>
    /// Loads the resume token across restarts (load-only — the source never commits).
    /// If <c>null</c>, the source starts from the current oplog position.
    /// The durable position is advanced downstream by a <c>CheckpointWriter</c> after the
    /// destination has persisted the records (at-least-once), never at emit time.
    /// </summary>
    public ICheckpointStore<string>? CheckpointStore { get; set; }

    /// <summary>
    /// Identifies this consumer's checkpoint in <see cref="CheckpointStore"/>. The same collection
    /// can be tailed by several consumers, each with its own id. Must match the
    /// <c>CheckpointId</c> of the paired <c>CheckpointWriter</c>.
    /// </summary>
    public string CheckpointId { get; set; } = null!;

    /// <summary>Maps a change stream document to the output type. Required.</summary>
    public Func<ChangeStreamDocument<BsonDocument>, TOutput> EventMapper { get; set; } = null!;

    /// <inheritdoc/>
    public override void Execute(CancellationToken cancellationToken)
    {
        LogStart();
        try
        {
            RunChangeStreamLoop(cancellationToken);
        }
        finally
        {
            Buffer.Complete();
            LogFinish();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void RunChangeStreamLoop(CancellationToken ct)
    {
        var resumeToken = LoadResumeToken(ct);

        var db = MongoClient.GetDatabase(Database);
        var collection = db.GetCollection<BsonDocument>(Collection);

        var options = new ChangeStreamOptions
        {
            FullDocument = FullDocument,
            MaxAwaitTime = MaxAwaitTime,
        };
        if (resumeToken != null)
        {
            options.ResumeAfter = resumeToken;
        }

        var pipeline =
            Pipeline ?? new EmptyPipelineDefinition<ChangeStreamDocument<BsonDocument>>();
        using var cursor = collection.Watch(pipeline, options, ct);

        while (!ct.IsCancellationRequested)
        {
            while (cursor.MoveNext(ct))
            {
                foreach (var doc in cursor.Current)
                {
                    ct.ThrowIfCancellationRequested();
                    var output = EventMapper(doc);
                    // Propagate the source's cancellation token into SendAsync so that
                    // backpressure from a bounded downstream buffer doesn't trap the
                    // change-stream loop after Cancel() — see RSSL-11703 regression test
                    // Execute_CancellationDuringBlockedSendAsync_ReturnsPromptly.
                    Buffer.SendAsync(output, ct).GetAwaiter().GetResult();
                    LogProgress();
                }
                // The durable resume token is NOT written here. A downstream CheckpointWriter
                // commits it after the destination persists (at-least-once); the source only
                // advances the live change-stream cursor in-memory. See ICheckpointStore.
            }
        }
    }

    private BsonDocument? LoadResumeToken(CancellationToken ct)
    {
        if (CheckpointStore == null)
            return null;
        var (found, json) = CheckpointStore.LoadAsync(CheckpointId, ct).GetAwaiter().GetResult();
        return found ? BsonDocument.Parse(json) : null;
    }
}
