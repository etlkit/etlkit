using System.Dynamic;
using EtlKit.Serialization.DataFlow;
using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Serialization.Tests;

public class ServiceProviderActivatorTests
{
    [Fact]
    public void CreateInstance_RegisteredService_ShouldResolveFromContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestService("injected"));
        var provider = services.BuildServiceProvider();
        var activator = new ServiceProviderActivator(provider);

        var result = activator.CreateInstance(typeof(TestService));

        Assert.NotNull(result);
        Assert.IsType<TestService>(result);
        Assert.Equal("injected", ((TestService)result!).Name);
    }

    [Fact]
    public void CreateInstance_UnregisteredTypeWithDefaultCtor_ShouldFallbackToActivatorUtilities()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var activator = new ServiceProviderActivator(provider);

        var result = activator.CreateInstance(typeof(SimpleClass));

        Assert.NotNull(result);
        Assert.IsType<SimpleClass>(result);
    }

    [Fact]
    public void CreateInstance_UnregisteredTypeWithDependency_ShouldInjectRegisteredDependencies()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestService("fromDI"));
        var provider = services.BuildServiceProvider();
        var activator = new ServiceProviderActivator(provider);

        var result = activator.CreateInstance(typeof(ClassWithDependency));

        Assert.NotNull(result);
        Assert.IsType<ClassWithDependency>(result);
        Assert.Equal("fromDI", ((ClassWithDependency)result!).Service.Name);
    }

    [Fact]
    public void CreateInstance_GenericTypeDefinition_ShouldConstructWithExpandoObject()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var activator = new ServiceProviderActivator(provider);

        var result = activator.CreateInstance(typeof(List<>));

        Assert.NotNull(result);
        Assert.IsType<List<ExpandoObject>>(result);
    }

    [Fact]
    public void CreateInstance_ConcreteGenericType_ShouldCreateDirectly()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var activator = new ServiceProviderActivator(provider);

        var result = activator.CreateInstance(typeof(List<int>));

        Assert.NotNull(result);
        Assert.IsType<List<int>>(result);
    }

    [Fact]
    public void Constructor_NullServiceProvider_ShouldThrowArgumentNullException()
    {
        var act = () => new ServiceProviderActivator(null!);

        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("serviceProvider", ex.ParamName);
    }

    public class TestService
    {
        public string Name { get; }

        public TestService(string name) => Name = name;
    }

    public class SimpleClass
    {
        public string Value { get; set; } = "default";
    }

    public class ClassWithDependency
    {
        public TestService Service { get; }

        public ClassWithDependency(TestService service) => Service = service;
    }
}
