using System;
using System.Threading;
using JetBrains.Annotations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EtlKit.DataFlow;

/// <summary>
/// Produces start positions for <see cref="MongoChangeStreamSource{TOutput}"/>.
/// </summary>
[PublicAPI]
public static class MongoChangeStreamPosition
{
    /// <summary>
    /// Snapshots the deployment's current cluster time, for use as
    /// <see cref="MongoChangeStreamSource{TOutput}.StartAtOperationTime"/>.
    /// </summary>
    /// <remarks>
    /// Take the snapshot before the writes that must not be missed. Do not substitute a client
    /// clock: a client running ahead of the deployment places the start position after writes that
    /// already happened, which is the cold-start gap this is meant to close.
    /// </remarks>
    /// <param name="client">Client connected to the deployment that will be watched.</param>
    /// <param name="database">Database used to issue the command.</param>
    /// <param name="cancellationToken">Token that cancels the command.</param>
    /// <returns>The deployment's cluster time, truncated to whole seconds.</returns>
    public static DateTimeOffset Current(
        IMongoClient client,
        string database,
        CancellationToken cancellationToken = default
    )
    {
        var reply = client
            .GetDatabase(database)
            .RunCommand<BsonDocument>(
                new BsonDocumentCommand<BsonDocument>(new BsonDocument("ping", 1)),
                cancellationToken: cancellationToken
            );
        return DateTimeOffset.FromUnixTimeSeconds(
            reply["$clusterTime"]["clusterTime"].AsBsonTimestamp.Timestamp
        );
    }

    /// <summary>
    /// Converts a point in time to the BSON timestamp a change stream starts from.
    /// </summary>
    /// <remarks>
    /// A BSON timestamp is (seconds, ordinal-within-that-second). The ordinal counts operations
    /// the server performed, so a wall-clock fraction cannot be mapped onto it and is discarded
    /// downwards — starting slightly early replays events, starting late loses them.
    /// Callers must check <see cref="IsRepresentable"/> first.
    /// </remarks>
    internal static BsonTimestamp ToBsonTimestamp(DateTimeOffset value) =>
        new((int)value.ToUnixTimeSeconds(), 0);

    /// <summary>
    /// Reports whether an instant fits the 32-bit seconds field of a BSON timestamp.
    /// </summary>
    internal static bool IsRepresentable(DateTimeOffset value) =>
        value.ToUnixTimeSeconds() is >= 0 and <= int.MaxValue;
}
