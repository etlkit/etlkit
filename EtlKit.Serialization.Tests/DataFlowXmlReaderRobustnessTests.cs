using System.Dynamic;
using System.Reflection;
using System.Text;
using System.Xml;
using EtlKit.DataFlow;
using EtlKit.Serialization.DataFlow;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Robustness tests for DataFlowXmlReader: ReflectionTypeLoadException handling
/// and non-generic interface parameter guard.
/// </summary>
public class DataFlowXmlReaderRobustnessTests
{
    [Fact]
    public void SafeGetTypes_NormalAssembly_ShouldReturnTypes()
    {
        // Arrange — invoke the private SafeGetTypes method via reflection
        var safeGetTypesMethod = typeof(DataFlowXmlReader).GetMethod(
            "SafeGetTypes",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(safeGetTypesMethod);

        // Act
        var result =
            safeGetTypesMethod.Invoke(null, [typeof(DataFlowXmlReader).Assembly]) as Type[];

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, t => t == typeof(DataFlowXmlReader));
    }

    [Fact]
    public void Constructor_WithTestAssembliesLoaded_ShouldNotThrow()
    {
        // Verifies that DataFlowXmlReader construction succeeds even when
        // potentially problematic assemblies are loaded in the AppDomain.
        // Before the fix, GetDataFlowTypes used a bare catch; now it uses SafeGetTypes.
        using var dataFlow = new EtlDataFlowStep();

        var reader = new DataFlowXmlReader(dataFlow);

        Assert.NotNull(reader);
    }

    [Fact]
    public void Deserialize_InterfacePropertyResolution_ShouldNotThrowReflectionTypeLoadException()
    {
        // Exercises the GetType → SafeGetTypes path during interface property resolution.
        var errorDest = new ErrorLogDestination();
        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        // Act — should not throw ReflectionTypeLoadException
        var step = DataFlowXmlReader.Deserialize<EtlDataFlowStep>(xml, errorDest);

        // Assert
        Assert.NotNull(step);
        Assert.NotNull(step.Source);
        Assert.Single(step.Destinations);
    }

    [Fact]
    public void Deserialize_SourceWithNonGenericInterfaceMethod_ShouldSkipMethodGracefully()
    {
        // MemorySourceWithNonGenericMethod has Accept(IDisposable) — a non-generic interface param.
        // Before the fix, GetGenericTypeDefinition() on IDisposable threw InvalidOperationException.
        // After the fix, the IsGenericType guard causes AddDestinationAndInvokeMethod to skip it.
        var xml =
            @"<EtlDataFlowStep>
                <MemorySourceWithNonGenericMethod>
                    <Accept>
                        <MemoryDestination />
                    </Accept>
                    <LinkTo>
                        <MemoryDestination />
                    </LinkTo>
                </MemorySourceWithNonGenericMethod>
            </EtlDataFlowStep>";

        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        using var dataFlow = new EtlDataFlowStep();
        var reader = new DataFlowXmlReader(dataFlow);

        // Act — should not throw InvalidOperationException
        reader.Read(xmlReader);

        // Assert — LinkTo destination was added, Accept was skipped
        Assert.NotNull(dataFlow.Source);
        Assert.Single(dataFlow.Destinations);
    }

    /// <summary>
    /// Memory source with a method accepting a non-generic interface parameter.
    /// Used to verify the IsGenericType guard in AddDestinationAndInvokeMethod.
    /// </summary>
    public class MemorySourceWithNonGenericMethod : MemorySource<ExpandoObject>
    {
        /// <summary>
        /// Method with non-generic interface parameter — triggers the guard.
        /// </summary>
        public void Accept(IDisposable target)
        {
            // No-op
        }
    }
}
