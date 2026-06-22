using JetBrains.Annotations;
using Testcontainers.PostgreSql;

namespace EtlKit.PostgresStreaming.Tests;

[UsedImplicitly]
public sealed class PostgresContainerFixture : IDisposable
{
    // Bound startup explicitly so a stuck Docker/Testcontainers state surfaces fast
    // instead of consuming the whole CI job timeout. See SYSOPS-1667 for the cluster-side
    // story (Ryuk was disabled cluster-wide, which removed the implicit death-timer that
    // would otherwise have failed StartAsync within seconds).
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly PostgreSqlContainer _container;

    public string ConnectionString => _container.GetConnectionString();

    public PostgresContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("etltest")
            .WithUsername("postgres")
            .WithPassword("etlkitpassword")
            .Build();
        using var cts = new CancellationTokenSource(StartupTimeout);
        try
        {
            _container.StartAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"PostgreSQL Testcontainers fixture failed to start within {StartupTimeout.TotalSeconds:F0}s. "
                    + "Check Docker daemon availability and image pull."
            );
        }
    }

    public void Dispose()
    {
        _container.DisposeAsync().AsTask().Wait(ShutdownTimeout);
    }
}
