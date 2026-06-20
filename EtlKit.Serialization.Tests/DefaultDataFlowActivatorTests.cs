using System.Dynamic;
using System.Globalization;
using EtlKit.Serialization.DataFlow;
using CsvHelper.Configuration;

namespace EtlKit.Serialization.Tests;

public class DefaultDataFlowActivatorTests
{
    private readonly DefaultDataFlowActivator _activator = new();

    [Fact]
    public void CreateInstance_StandardType_ShouldCreateInstance()
    {
        var result = _activator.CreateInstance(typeof(List<string>));

        Assert.NotNull(result);
        Assert.IsType<List<string>>(result);
    }

    [Fact]
    public void CreateInstance_CsvConfiguration_ShouldCreateWithInvariantCulture()
    {
        var result = _activator.CreateInstance(typeof(CsvConfiguration));

        Assert.NotNull(result);
        Assert.IsType<CsvConfiguration>(result);
        var config = (CsvConfiguration)result!;
        Assert.Equal(CultureInfo.InvariantCulture, config.CultureInfo);
    }

    [Fact]
    public void CreateInstance_GenericTypeDefinition_ShouldConstructWithExpandoObject()
    {
        var result = _activator.CreateInstance(typeof(List<>));

        Assert.NotNull(result);
        Assert.IsType<List<ExpandoObject>>(result);
    }

    [Fact]
    public void CreateInstance_ConcreteGenericType_ShouldCreateDirectly()
    {
        var result = _activator.CreateInstance(typeof(List<int>));

        Assert.NotNull(result);
        Assert.IsType<List<int>>(result);
    }

    [Fact]
    public void CreateInstance_ExpandoObject_ShouldCreateInstance()
    {
        var result = _activator.CreateInstance(typeof(ExpandoObject));

        Assert.NotNull(result);
        Assert.IsType<ExpandoObject>(result);
    }
}
