using JetBrains.Annotations;
using Testcontainers.MongoDb;

namespace EtlKit.MongoDB.Tests;

[UsedImplicitly]
#pragma warning disable SP3110
public sealed class MongoContainerFixture : IDisposable
#pragma warning restore SP3110
{
    // Bound startup explicitly so a stuck Docker/Testcontainers state surfaces fast
    // instead of consuming the whole CI job timeout. See SYSOPS-1667 for the cluster-side
    // story (Ryuk was disabled cluster-wide, which removed the implicit death-timer that
    // would otherwise have failed StartAsync within seconds).
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly MongoDbContainer _container;

    public string ConnectionString => _container.GetConnectionString();

    public MongoContainerFixture()
    {
        _container = new MongoDbBuilder("mongo:6.0").WithReplicaSet().Build();
        using var cts = new CancellationTokenSource(StartupTimeout);
        try
        {
            _container.StartAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"MongoDB Testcontainers fixture failed to start within {StartupTimeout.TotalSeconds:F0}s. "
                    + "Check Docker daemon availability and image pull (mongo:6.0)."
            );
        }
    }

    public void Dispose()
    {
        _container.DisposeAsync().AsTask().Wait(ShutdownTimeout);
    }
}
