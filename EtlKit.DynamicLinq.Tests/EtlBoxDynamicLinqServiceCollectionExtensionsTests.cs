using EtlKit.DynamicLinq;
using EtlKit.DynamicLinq.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.DynamicLinq.Tests;

public class EtlBoxDynamicLinqServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEtlBoxDynamicLinq_RegistersNonGeneric_AsTransient()
    {
        var services = new ServiceCollection();

        services.AddEtlBoxDynamicLinq();

        var descriptor = services.Single(d => d.ServiceType == typeof(ExpressionRowFiltration));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(ExpressionRowFiltration), descriptor.ImplementationType);
    }

    [Fact]
    public void AddEtlBoxDynamicLinq_RegistersOpenGeneric_AsTransient()
    {
        var services = new ServiceCollection();

        services.AddEtlBoxDynamicLinq();

        var descriptor = services.Single(d => d.ServiceType == typeof(ExpressionRowFiltration<>));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(ExpressionRowFiltration<>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddEtlBoxDynamicLinq_ReturnsSameServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddEtlBoxDynamicLinq();

        Assert.Same(services, returned);
    }
}
