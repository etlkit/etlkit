using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using EtlKit.Common.ControlFlow;
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

    /// <summary>
    /// Cold-start seed: starts the change stream at this point in time. Ignored when
    /// <see cref="CheckpointStore"/> yields a token. Snapshot it with
    /// <see cref="MongoChangeStreamPosition.Current"/> rather than reading a client clock — a
    /// client running ahead of the deployment would reintroduce the gap this closes.
    /// Mutually exclusive with <see cref="StartAfter"/>. Resolved to whole seconds, so the stream
    /// may replay events from within the snapshotted second (at-least-once, as documented).
    /// </summary>
    public DateTimeOffset? StartAtOperationTime { get; set; }

    /// <summary>
    /// Cold-start seed: starts the change stream strictly after this resume token, given in the
    /// same JSON form a <c>CheckpointWriter</c> commits (<c>doc.ResumeToken.ToJson()</c>).
    /// Ignored when <see cref="CheckpointStore"/> yields a token. Mutually exclusive with
    /// <see cref="StartAtOperationTime"/>.
    /// </summary>
    public string? StartAfter { get; set; }

    /// <summary>
    /// Controls how a token loaded from <see cref="CheckpointStore"/> is applied. Switch to
    /// <see cref="ChangeStreamResumeMode.StartAfter"/> to resume past an <c>invalidate</c> event,
    /// which <c>resumeAfter</c> cannot do.
    /// </summary>
    public ChangeStreamResumeMode CheckpointResumeMode { get; set; } =
        ChangeStreamResumeMode.ResumeAfter;

    /// <summary>Maps a change stream document to the output type. Required.</summary>
    public Func<ChangeStreamDocument<BsonDocument>, TOutput> EventMapper { get; set; } = null!;

    /// <inheritdoc/>
    public override void Execute(CancellationToken cancellationToken)
    {
        LogStart();
        try
        {
            // Inside the try so that a rejected configuration still completes the buffer —
            // otherwise a linked destination waits forever on a pipeline that never started.
            ValidateStartPosition();
            RunChangeStreamLoop(cancellationToken);
        }
        finally
        {
            Buffer.Complete();
            LogFinish();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateStartPosition()
    {
        if (StartAfter != null && StartAtOperationTime != null)
        {
            throw new InvalidOperationException(
                "MongoChangeStreamSource: StartAfter and StartAtOperationTime are mutually "
                    + "exclusive start positions. Set at most one of them."
            );
        }

        if (
            StartAtOperationTime is { } startAt
            && !MongoChangeStreamPosition.IsRepresentable(startAt)
        )
        {
            throw new InvalidOperationException(
                $"MongoChangeStreamSource: StartAtOperationTime ({startAt:O}) is outside the range "
                    + "a BSON timestamp can represent (1970-01-01 to 2038-01-19, UTC)."
            );
        }
    }

    private void RunChangeStreamLoop(CancellationToken ct)
    {
        var db = MongoClient.GetDatabase(Database);
        var collection = db.GetCollection<BsonDocument>(Collection);

        var options = new ChangeStreamOptions
        {
            FullDocument = FullDocument,
            MaxAwaitTime = MaxAwaitTime,
        };
        ApplyStartPosition(options, ct);

        var pipeline =
            Pipeline ?? new EmptyPipelineDefinition<ChangeStreamDocument<BsonDocument>>();
        using var cursor = collection.Watch(pipeline, options, ct);

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

        // MoveNext returned false: the server closed the cursor, which is what happens after an
        // invalidate event (the watched collection was dropped or renamed). This used to sit
        // inside an outer while(!cancelled) loop that re-entered the dead cursor immediately,
        // burning a core until cancellation. Stop instead — resuming past an invalidate is only
        // legal via startAfter, and doing it implicitly would silently start reading a brand-new
        // collection that happens to reuse the old name.
        // That is the caller's call, made by setting CheckpointResumeMode to
        // ChangeStreamResumeMode.StartAfter.
        LogCursorClosed();
    }

    private void LogCursorClosed()
    {
        if (DisableLogging)
            return;
        Logger.Warn(
            TaskName
                + " change stream cursor was closed by the server (e.g. after an invalidate event); the source stopped.",
            TaskType,
            "LOG",
            TaskHash,
            Common.ControlFlow.ControlFlow.Stage,
            Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
        );
    }

    // MongoDB treats resumeAfter, startAfter and startAtOperationTime as mutually exclusive, so
    // exactly one of them is ever set. A committed checkpoint always outranks the configured
    // seeds: on a restart the consumer must continue from real progress, not replay from a value
    // that has been sitting in configuration since the first run.
    private void ApplyStartPosition(ChangeStreamOptions options, CancellationToken ct)
    {
        var checkpointToken = LoadResumeToken(ct);
        if (checkpointToken != null)
        {
            if (CheckpointResumeMode == ChangeStreamResumeMode.StartAfter)
                options.StartAfter = checkpointToken;
            else
                options.ResumeAfter = checkpointToken;
            return;
        }

        if (StartAfter != null)
        {
            options.StartAfter = BsonDocument.Parse(StartAfter);
            return;
        }

        if (StartAtOperationTime is { } startAt)
        {
            options.StartAtOperationTime = MongoChangeStreamPosition.ToBsonTimestamp(startAt);
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
