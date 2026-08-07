using JetBrains.Annotations;
using Xunit;

namespace EtlKit.MongoDB.Tests;

// One MongoDB container for every test class in this assembly. Without this definition the
// [Collection("MongoDB")] attributes are inert and each class starts its own container.
[UsedImplicitly]
[CollectionDefinition("MongoDB")]
public sealed class MongoDbCollection : ICollectionFixture<MongoContainerFixture>;
