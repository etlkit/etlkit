using EtlKit.DataFlow;
using MongoDB.Driver;
using Xunit;
using static EtlKit.MongoDB.Tests.MongoTestHelpers;

namespace EtlKit.MongoDB.Tests;

public sealed class MongoChangeStreamPositionConversionTests
{
    // A BSON timestamp is (seconds since epoch, ordinal within that second). The ordinal is a
    // server-assigned operation counter, not a fraction, so a sub-second remainder has no correct
    // target and is dropped. It must be dropped DOWNWARDS: rounding up would place the start
    // position after operations that already happened and silently lose them.
    [Fact]
    public void ToBsonTimestamp_TruncatesSubSecondRemainderDownwards()
    {
        var wholeSecond = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var withRemainder = wholeSecond.AddMilliseconds(999).AddTicks(9);

        var result = MongoChangeStreamPosition.ToBsonTimestamp(withRemainder);

        Assert.Equal((int)wholeSecond.ToUnixTimeSeconds(), result.Timestamp);
        Assert.Equal(0, result.Increment);
    }

    [Fact]
    public void ToBsonTimestamp_ExactSecond_KeepsTheSecondAndZeroesTheIncrement()
    {
        var wholeSecond = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

        var result = MongoChangeStreamPosition.ToBsonTimestamp(wholeSecond);

        Assert.Equal((int)wholeSecond.ToUnixTimeSeconds(), result.Timestamp);
        Assert.Equal(0, result.Increment);
    }

    [Fact]
    public void ToBsonTimestamp_NormalisesOffsetToUtc()
    {
        var utc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var sameInstantElsewhere = new DateTimeOffset(2026, 8, 7, 13, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal(
            MongoChangeStreamPosition.ToBsonTimestamp(utc).Timestamp,
            MongoChangeStreamPosition.ToBsonTimestamp(sameInstantElsewhere).Timestamp
        );
    }

    [Fact]
    public void IsRepresentable_RejectsInstantsOutsideTheBsonTimestampRange()
    {
        // The seconds field of a BSON timestamp is a 32-bit signed integer.
        Assert.False(
            MongoChangeStreamPosition.IsRepresentable(
                new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero)
            )
        );
        Assert.False(
            MongoChangeStreamPosition.IsRepresentable(
                DateTimeOffset.FromUnixTimeSeconds(int.MaxValue).AddSeconds(1)
            )
        );
        Assert.True(
            MongoChangeStreamPosition.IsRepresentable(
                new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero)
            )
        );
    }
}

[Collection("MongoDB")]
public sealed class MongoChangeStreamPositionTests
{
    private readonly MongoContainerFixture _fixture;

    public MongoChangeStreamPositionTests(MongoContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Current_ReturnsATimestampFromTheDeployment()
    {
        var client = new MongoClient(_fixture.ConnectionString);

        var snapped = MongoChangeStreamPosition.Current(client, DatabaseName);

        // Deliberately loose: this proves a real server timestamp came back, not that the
        // container's clock and the host's are synchronised.
        var delta = (DateTimeOffset.UtcNow - snapped).Duration();
        Assert.True(
            delta < TimeSpan.FromMinutes(5),
            $"Snapped cluster time {snapped:O} is {delta} away from the local clock."
        );
    }

    [Fact]
    public void Current_NeverGoesBackwards()
    {
        var client = new MongoClient(_fixture.ConnectionString);

        var first = MongoChangeStreamPosition.Current(client, DatabaseName);
        var second = MongoChangeStreamPosition.Current(client, DatabaseName);

        Assert.True(second >= first, $"Cluster time went backwards: {first:O} then {second:O}.");
    }
}
