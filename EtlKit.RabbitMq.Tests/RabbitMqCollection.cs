namespace EtlKit.RabbitMq.Tests;

[CollectionDefinition(nameof(RabbitMqCollection))]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
