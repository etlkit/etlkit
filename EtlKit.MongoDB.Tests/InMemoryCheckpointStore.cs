using System;
using System.Collections.Concurrent;
using EtlKit.Common.DataFlow.Streaming;

namespace EtlKit.MongoDB.Tests;

internal sealed class InMemoryCheckpointStore<TPosition> : ICheckpointStore<TPosition>
    where TPosition : IComparable<TPosition>
{
    private readonly ConcurrentDictionary<string, TPosition> _positions = new();

    public int CommitCount { get; private set; }

    public Task<(bool Found, TPosition Position)> LoadAsync(
        string checkpointId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _positions.TryGetValue(checkpointId, out var p)
                ? (true, p)
                : (false, default(TPosition)!)
        );

    public Task CommitAsync(string checkpointId, TPosition position, CancellationToken ct)
    {
        _positions[checkpointId] = position;
        CommitCount++;
        return Task.CompletedTask;
    }
}
