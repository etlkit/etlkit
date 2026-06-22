using System.Reflection;
using System.Xml.Linq;
using EtlKit.Serialization.DataFlow;

namespace EtlKit.Serialization.Tests;

/// <summary>
/// Tests for DataFlowXmlReader deserialization of IDictionary&lt;string, object?&gt; fields
/// </summary>
public class DataFlowXmlReaderTests
{
    [Fact]
    public void DataFlowXmlReader_SimpleValues_ShouldDeserializeToIDictionary()
    {
        // Arrange
        var xml =
            @"
            <TestClassWithDictionary>
                <Name>TestName</Name>
                <Parameters>
                    <Parameter1>Value1</Parameter1>
                    <Parameter2>Value2</Parameter2>
                    <Parameter3>Value3</Parameter3>
                </Parameters>
            </TestClassWithDictionary>";

        // Act
        var result = DeserializeXml(xml);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
        Assert.NotNull(result.Parameters);
        Assert.Equal(3, result.Parameters.Count);
        Assert.Equal("Value1", result.Parameters["Parameter1"]);
        Assert.Equal("Value2", result.Parameters["Parameter2"]);
        Assert.Equal("Value3", result.Parameters["Parameter3"]);
    }

    [Fact]
    public void DataFlowXmlReader_NestedObjects_ShouldDeserializeToIDictionary()
    {
        // Arrange
        var xml =
            @"
            <TestClassWithDictionary>
                <Name>TestName</Name>
                <Parameters>
                    <Parameter1>Value1</Parameter1>
                    <Parameter2>Value2</Parameter2>
                    <NestedObject>
                        <NestedKey1>NestedValue1</NestedKey1>
                        <NestedKey2>NestedValue2</NestedKey2>
                    </NestedObject>
                </Parameters>
            </TestClassWithDictionary>";

        // Act
        var result = DeserializeXml(xml);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
        Assert.NotNull(result.Parameters);
        Assert.Equal(3, result.Parameters.Count);
        Assert.Equal("Value1", result.Parameters["Parameter1"]);
        Assert.Equal("Value2", result.Parameters["Parameter2"]);

        var nestedObj = result.Parameters["NestedObject"] as IDictionary<string, object?>;
        Assert.NotNull(nestedObj);
        Assert.Equal("NestedValue1", nestedObj["NestedKey1"]);
        Assert.Equal("NestedValue2", nestedObj["NestedKey2"]);
    }

    [Fact]
    public void DataFlowXmlReader_ComplexNesting_ShouldDeserializeToIDictionary()
    {
        // Arrange
        var xml =
            @"
            <TestClassWithDictionary>
                <Name>ComplexTest</Name>
                <Parameters>
                    <SimpleValue>test</SimpleValue>
                    <Level1>
                        <Level2>
                            <Level3>
                                <DeepValue>deeply nested value</DeepValue>
                            </Level3>
                        </Level2>
                        <Level2Sibling>sibling value</Level2Sibling>
                    </Level1>
                    <Array>
                        <Item>item1</Item>
                        <Item>item2</Item>
                        <Item>item3</Item>
                    </Array>
                </Parameters>
            </TestClassWithDictionary>";

        // Act
        var result = DeserializeXml(xml);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ComplexTest", result.Name);
        Assert.NotNull(result.Parameters);
        Assert.Equal("test", result.Parameters["SimpleValue"]);

        var level1 = result.Parameters["Level1"] as IDictionary<string, object?>;
        Assert.NotNull(level1);
        Assert.Equal("sibling value", level1["Level2Sibling"]);

        var level2 = level1["Level2"] as IDictionary<string, object?>;
        Assert.NotNull(level2);

        var level3 = level2["Level3"] as IDictionary<string, object?>;
        Assert.NotNull(level3);
        Assert.Equal("deeply nested value", level3["DeepValue"]);

        var array = result.Parameters["Array"] as IDictionary<string, object?>;
        Assert.NotNull(array);
        Assert.True(array.ContainsKey("Item"));
    }

    [Fact]
    public void DataFlowXmlReader_EmptyElement_ShouldDeserializeAsNull()
    {
        // Arrange
        var xml =
            @"
            <TestClassWithDictionary>
                <Name>EmptyTest</Name>
                <Parameters>
                    <EmptyValue></EmptyValue>
                    <NonEmptyValue>test</NonEmptyValue>
                </Parameters>
            </TestClassWithDictionary>";

        // Act
        var result = DeserializeXml(xml);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EmptyTest", result.Name);
        Assert.NotNull(result.Parameters);
        Assert.Single(result.Parameters);
        Assert.False(result.Parameters.ContainsKey("EmptyValue"));
        Assert.Equal("test", result.Parameters["NonEmptyValue"]);
    }

    [Fact]
    public void DataFlowXmlReader_MixedTypes_ShouldDeserializeCorrectly()
    {
        // Arrange
        var xml =
            @"
            <TestClassWithDictionary>
                <Name>MixedTypesTest</Name>
                <Parameters>
                    <StringValue>text value</StringValue>
                    <NumberValue>12345</NumberValue>
                    <Config>
                        <Enabled>true</Enabled>
                        <MaxRetries>3</MaxRetries>
                        <Nested>
                            <ApiKey>secret-key</ApiKey>
                            <Timeout>30</Timeout>
                        </Nested>
                    </Config>
                </Parameters>
            </TestClassWithDictionary>";

        // Act
        var result = DeserializeXml(xml);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Parameters);
        Assert.Equal("text value", result.Parameters["StringValue"]);
        Assert.Equal("12345", result.Parameters["NumberValue"]);

        var config = result.Parameters["Config"] as IDictionary<string, object?>;
        Assert.NotNull(config);
        Assert.Equal("true", config["Enabled"]);
        Assert.Equal("3", config["MaxRetries"]);

        var nested = config["Nested"] as IDictionary<string, object?>;
        Assert.NotNull(nested);
        Assert.Equal("secret-key", nested["ApiKey"]);
        Assert.Equal("30", nested["Timeout"]);
    }

    [Fact]
    public void DataFlowXmlReader_SimpleValuesWithNumbers_ShouldDeserializeAsStrings()
    {
        // Arrange - verify that simple values in IDictionary<string, object?> are deserialized as strings
        // This is expected behavior because XML has no type information
        var xml =
            @"
            <TestClassWithDictionary>
                <Name>TypeTest</Name>
                <Parameters>
                    <IntValue>42</IntValue>
                    <DecimalValue>3.14</DecimalValue>
                    <BoolValue>true</BoolValue>
                    <DateValue>2025-01-26</DateValue>
                    <TextValue>hello</TextValue>
                </Parameters>
            </TestClassWithDictionary>";

        // Act
        var result = DeserializeXml(xml);

        // Assert - all values are strings (XML has no type metadata)
        Assert.NotNull(result);
        Assert.NotNull(result.Parameters);
        Assert.Equal(5, result.Parameters.Count);

        // All simple values are deserialized as strings
        Assert.IsType<string>(result.Parameters["IntValue"]);
        Assert.Equal("42", result.Parameters["IntValue"]);

        Assert.IsType<string>(result.Parameters["DecimalValue"]);
        Assert.Equal("3.14", result.Parameters["DecimalValue"]);

        Assert.IsType<string>(result.Parameters["BoolValue"]);
        Assert.Equal("true", result.Parameters["BoolValue"]);

        Assert.IsType<string>(result.Parameters["DateValue"]);
        Assert.Equal("2025-01-26", result.Parameters["DateValue"]);

        Assert.IsType<string>(result.Parameters["TextValue"]);
        Assert.Equal("hello", result.Parameters["TextValue"]);
    }

    private static TestClassWithDictionary DeserializeXml(string xml)
    {
        var xElement = XElement.Parse(xml);
        using var mockDataFlow = new EtlDataFlowStep();
        var reader = new DataFlowXmlReader(mockDataFlow);
        var createObjectMethod = typeof(DataFlowXmlReader).GetMethod(
            "CreateObject",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        return createObjectMethod!.Invoke(reader, [typeof(TestClassWithDictionary), xElement])
                as TestClassWithDictionary
            ?? throw new InvalidOperationException("Deserialization failed");
    }

    /// <summary>
    /// Test class for testing DataFlowXmlReader deserialization of IDictionary&lt;string, object?&gt;
    /// </summary>
    private class TestClassWithDictionary
    {
        public IDictionary<string, object?>? Parameters { get; set; } = null;
        public string? Name { get; set; } = null;
    }
}
