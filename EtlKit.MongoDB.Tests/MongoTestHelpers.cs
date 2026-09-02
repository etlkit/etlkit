using MongoDB.Bson;
using MongoDB.Driver;

namespace EtlKit.MongoDB.Tests;

// Shared by every Mongo test class in this assembly. Reach them with
// `using static EtlKit.MongoDB.Tests.MongoTestHelpers;` so call sites read unqualified.
internal static class MongoTestHelpers
{
    public const string DatabaseName = "etltest";

    public static IMongoCollection<BsonDocument> GetCollection(IMongoClient client, string name)
    {
        var db = client.GetDatabase(DatabaseName);
        var collection = db.GetCollection<BsonDocument>(name);
        collection.DeleteMany(FilterDefinition<BsonDocument>.Empty);
        return collection;
    }

    public static void WaitForResults<T>(List<T> results, int expectedCount, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (results.Count < expectedCount && DateTime.UtcNow < deadline)
            Thread.Sleep(30);
    }
}
