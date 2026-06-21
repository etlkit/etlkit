using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Extensions;
using EtlKit.Serialization.DataFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Regression tests for DbMerge deserialization with various XML element orderings.
/// Root cause: when DbMerge was created via the DI constructor DbMerge(ILogger), the
/// BatchSize property remained 0 (default int), causing ArgumentOutOfRangeException
/// when setting TableName — because that immediately creates a DbDestination with BatchSize=0.
/// </summary>
public class DbMergeDeserializationTests
{
    /// <summary>
    /// Direct bug reproduction: create DbMerge via the ILogger constructor (as ServiceProviderActivator does),
    /// then set TableName without setting BatchSize first.
    /// </summary>
    [Fact]
    public void DbMerge_CreatedViaLoggerConstructor_TableNameSetFirst_NoException()
    {
        // Arrange — this is exactly the creation path used by ServiceProviderActivator when ILogger is in DI
        var logger = NullLogger<DbMerge<ExpandoObject>>.Instance;
        var dbMerge = new DbMerge<ExpandoObject>(logger);

        // Act & Assert — before the fix: ArgumentOutOfRangeException when BatchSize=0 → BoundedCapacity=0
        var exception = Record.Exception(() => dbMerge.TableName = "TestTable");
        Assert.Null(exception);
    }

    /// <summary>
    /// DbMerge(ILogger) does not set BatchSize explicitly — the backing field should be initialized to DefaultBatchSize.
    /// </summary>
    [Fact]
    public void DbMerge_CreatedViaLoggerConstructor_BatchSizeIsDefault()
    {
        var logger = NullLogger<DbMerge<ExpandoObject>>.Instance;
        var dbMerge = new DbMerge<ExpandoObject>(logger);

        Assert.Equal(DbDestination.DefaultBatchSize, dbMerge.BatchSize);
    }

    /// <summary>
    /// If BatchSize is set AFTER TableName, the internal DestinationTable should also be updated.
    /// </summary>
    [Fact]
    public void DbMerge_BatchSizeSetAfterTableName_UpdatesDestinationTable()
    {
        var logger = NullLogger<DbMerge<ExpandoObject>>.Instance;
        var dbMerge = new DbMerge<ExpandoObject>(logger);
        dbMerge.TableName = "TestTable";

        dbMerge.BatchSize = 500;

        Assert.Equal(500, dbMerge.BatchSize);

        // Verify that BatchSize is synchronized with the private DestinationTable
        var destinationTable = (DbDestination<ExpandoObject>?)
            typeof(DbMerge<ExpandoObject>)
                .GetProperty("DestinationTable", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(dbMerge);

        Assert.NotNull(destinationTable);
        Assert.Equal(500, destinationTable.BatchSize);
    }

    /// <summary>
    /// XML deserialization with ServiceProvider (ILogger registered): TableName before BatchSize.
    /// Reproduces the real-world scenario from EtlDataFlowStep.RecreateDataFlow().
    /// </summary>
    [Fact]
    public void DbMerge_XmlDeserialization_WithServiceProvider_TableNameBeforeBatchSize_NoException()
    {
        // Arrange — ServiceProviderActivator will get ILogger<DbMerge<ExpandoObject>> from DI
        // and create DbMerge via the ILogger constructor, as in a real application
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitCore();
        var provider = services.BuildServiceProvider();

        // TableName comes BEFORE BatchSize — this ordering was what caused the crash
        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <DbMerge>
                            <TableName>TestTable</TableName>
                            <BatchSize>500</BatchSize>
                        </DbMerge>
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        // Act — use DataFlowXmlReader directly with null linkAllErrorsTo
        var exception = Record.Exception(() => DeserializeWithServiceProvider(xml, provider));
        Assert.Null(exception);
    }

    /// <summary>
    /// XML deserialization with ServiceProvider: BatchSize is absent from XML.
    /// DefaultBatchSize should be used, with no exceptions.
    /// </summary>
    [Fact]
    public void DbMerge_XmlDeserialization_WithServiceProvider_WithoutBatchSize_UsesDefault()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEtlKitCore();
        var provider = services.BuildServiceProvider();

        // BatchSize is not specified in XML
        var xml =
            @"<EtlDataFlowStep>
                <MemorySource>
                    <LinkTo>
                        <DbMerge>
                            <TableName>TestTable</TableName>
                        </DbMerge>
                    </LinkTo>
                </MemorySource>
            </EtlDataFlowStep>";

        EtlDataFlowStep step = null!;
        var exception = Record.Exception(
            () => step = DeserializeWithServiceProvider(xml, provider)
        );

        Assert.Null(exception);
        Assert.NotNull(step.Source);
    }

    private static EtlDataFlowStep DeserializeWithServiceProvider(
        string xml,
        IServiceProvider provider
    )
    {
        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        var step = new EtlDataFlowStep();
        // linkAllErrorsTo = null: do not call LinkErrorTo since DbMerge.TransformBlock is not initialized
        var reader = new DataFlowXmlReader(
            step,
            CultureInfo.InvariantCulture,
            linkAllErrorsTo: null,
            serviceProvider: provider
        );
        reader.Read(xmlReader);
        return step;
    }

    /// <summary>
    /// A DbDestination created with batchSize=0 should not throw an exception
    /// and should use DefaultBatchSize.
    /// </summary>
    [Fact]
    public void DbDestination_BatchSizeSetToZero_UsesDefaultBatchSize()
    {
        // Act & Assert — before the fix: ArgumentOutOfRangeException
        DbDestination<ExpandoObject>? dest = null;
        var exception = Record.Exception(
            () => dest = new DbDestination<ExpandoObject>(batchSize: 0)
        );

        Assert.Null(exception);
        Assert.NotNull(dest);
        Assert.Equal(DataFlowBatchDestination<ExpandoObject>.DefaultBatchSize, dest.BatchSize);
    }

    /// <summary>
    /// A DbDestination created with a valid batchSize should preserve that value.
    /// </summary>
    [Fact]
    public void DbDestination_BatchSizeSetToPositive_PreservesValue()
    {
        var dest = new DbDestination<ExpandoObject>(batchSize: 250);

        Assert.Equal(250, dest.BatchSize);
    }
}
