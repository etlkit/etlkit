#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace EtlKit.Common.DataFlow.Streaming;

/// <summary>
/// Persists and retrieves a strongly-typed streaming cursor, keyed by <c>checkpointId</c>.
/// </summary>
/// <typeparam name="TPosition">
/// The stream-position type. It is a single monotone cursor value (e.g. <see cref="long"/> for a
/// sequence column, or a resume-token string), hence the <see cref="IComparable{T}"/> constraint —
/// committers advance the checkpoint strictly forward and never regress it.
/// </typeparam>
/// <remarks>
/// <para>
/// <c>checkpointId</c> identifies a single consumer's progress over a stream, not the stream
/// itself. The same underlying stream can be tailed by several independent consumers, each with its
/// own <c>checkpointId</c> and therefore its own cursor — the Kafka consumer-group model. One store
/// instance can hold many checkpoints keyed this way.
/// </para>
/// <para>
/// Commit semantics: <see cref="CommitAsync"/> must be called only after the records up to
/// <c>position</c> have been durably handled downstream (e.g. written to the destination), never at
/// emit time. This is what yields at-least-once delivery — see <c>CheckpointWriter&lt;,&gt;</c>.
/// </para>
/// </remarks>
[PublicAPI]
public interface ICheckpointStore<TPosition>
    where TPosition : IComparable<TPosition>
{
    /// <summary>
    /// Returns the last committed position for <paramref name="checkpointId"/>.
    /// <c>Found</c> is <c>false</c> (and <c>Position</c> is <c>default</c>) if this checkpoint has
    /// never been committed — the source then starts from the beginning of the stream.
    /// </summary>
    Task<(bool Found, TPosition Position)> LoadAsync(string checkpointId, CancellationToken ct);

    /// <summary>
    /// Durably records <paramref name="position"/> as the committed cursor for
    /// <paramref name="checkpointId"/>. Call after downstream durability, never on emit.
    /// </summary>
    Task CommitAsync(string checkpointId, TPosition position, CancellationToken ct);
}
