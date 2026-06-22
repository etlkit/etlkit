using EtlKit.Common.ControlFlow;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Xunit.Abstractions;

namespace EtlKit.RabbitMq.Tests;

public abstract class RabbitMqTestBase
{
    protected RabbitMqFixture Fixture;
    protected readonly IConnectionFactory ConnectionFactory;

    protected RabbitMqTestBase(RabbitMqFixture fixture, ITestOutputHelper logger)
    {
        Fixture = fixture;
        ConnectionFactory = Fixture.GetConnectionFactory();
        Common.ControlFlow.ControlFlow.LoggerFactory = new LoggerFactory(
            new[] { new TestOutputLoggerProvider(logger) }
        );
    }
}
