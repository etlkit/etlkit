using EtlKit.DynamicLinq;
using EtlKit.DynamicLinq.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.DynamicLinq.Tests;

public class EtlKitDynamicLinqServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEtlKitDynamicLinq_RegistersNonGeneric_AsTransient()
    {
        var services = new ServiceCollection();

        services.AddEtlKitDynamicLinq();

        var descriptor = services.Single(d => d.ServiceType == typeof(ExpressionRowFiltration));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(ExpressionRowFiltration), descriptor.ImplementationType);
    }

    [Fact]
    public void AddEtlKitDynamicLinq_RegistersOpenGeneric_AsTransient()
    {
        var services = new ServiceCollection();

        services.AddEtlKitDynamicLinq();

        var descriptor = services.Single(d => d.ServiceType == typeof(ExpressionRowFiltration<>));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(ExpressionRowFiltration<>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddEtlKitDynamicLinq_ReturnsSameServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddEtlKitDynamicLinq();

        Assert.Same(services, returned);
    }
}
