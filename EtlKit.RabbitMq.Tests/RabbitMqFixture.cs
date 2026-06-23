using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace EtlKit.RabbitMq.Tests;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder().Build();

    public Task InitializeAsync()
    {
        return _rabbitMqContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return _rabbitMqContainer.DisposeAsync().AsTask();
    }

    public string GetConnectionString()
    {
        
        return _rabbitMqContainer.GetConnectionString();
    }

    public IConnectionFactory GetConnectionFactory() => new ConnectionFactory
    {
        Uri = new Uri(GetConnectionString())
    };

    public IConnectionFactory GetConnectionFactory(string connectionString) => new ConnectionFactory
    {
        Uri = new Uri(connectionString)
    };
}
