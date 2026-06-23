using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace EtlKit.RabbitMq.Tests;

public sealed class TestOutputLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _logger;

    public TestOutputLoggerProvider(ITestOutputHelper logger)
    {
        _logger = logger;
    }


    public ILogger CreateLogger(string categoryName)
    {
        return new TestOutputLogger(_logger);
    }

    public void Dispose()
    {
    }
}

internal class TestOutputLogger : ILogger
{
    private readonly ITestOutputHelper _logger;

    public TestOutputLogger(ITestOutputHelper logger)
    {
        _logger = logger;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return new Mock<IDisposable>().Object;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        _logger.WriteLine($"{logLevel}, {eventId}, {state}: {formatter(state, exception)}");
    }
}
