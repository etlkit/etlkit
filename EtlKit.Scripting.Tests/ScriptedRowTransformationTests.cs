using System.Dynamic;
using System.Reflection;
using EtlKit.DataFlow;
using EtlKit.Scripting;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace EtlKit.Scripting.Tests;

public class ScriptedRowTransformationTests
{
    [Fact]
    public void ShouldTransformExpandoObject()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>();
        script.Mappings.Add("NewId", "Id + 1");
        script.Mappings.Add("NewName", "$\"New{Name}\"");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);
        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();
        // Assert
        Assert.Single(
            memoryDestination.Data,
            (dynamic row) => row.NewId == 2 && row.NewName == "NewTest"
        );
    }

    [Fact]
    public void ShouldNotFailWhenFailOnMissingFieldIsFalseAndNestedPayloadChangedToEmpty()
    {
        // Arrange
        var itemWithPayload = new ExpandoObject() as IDictionary<string, object?>;
        itemWithPayload["meta"] = new ExpandoObject();
        itemWithPayload["payload"] = new ExpandoObject();
        var payload = (IDictionary<string, object?>)itemWithPayload["payload"]!;
        payload["ID"] = "7d51954d-5a06-4226-b7a3-defcdf0246a4";

        var itemWithEmptyPayload = new ExpandoObject() as IDictionary<string, object?>;
        itemWithEmptyPayload["meta"] = new ExpandoObject();
        itemWithEmptyPayload["payload"] = new ExpandoObject();

        var memorySource = new MemorySource();
        memorySource.DataAsList.Add((ExpandoObject)itemWithPayload);
        memorySource.DataAsList.Add((ExpandoObject)itemWithEmptyPayload);

        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            FailOnMissingField = false,
        };
        script.Mappings.Add("ResultId", "payload.ID");

        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert
        Assert.Equal(2, memoryDestination.Data.Count);
        var list = memoryDestination.Data.ToList();
        var first = (IDictionary<string, object?>)list[0];
        var second = (IDictionary<string, object?>)list[1];
        Assert.Equal("7d51954d-5a06-4226-b7a3-defcdf0246a4", first["ResultId"]);
        Assert.Null(second["ResultId"]);
    }

    [Fact]
    public void ShouldTransformExpandoObjectWithMissingFieldOnSource()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            FailOnMissingField = false,
        };
        script.Mappings.Add("NewId", "Id + 1");
        script.Mappings.Add("NewName", "$\"New{Name}\"");
        script.Mappings.Add("NewMissingField", "$\"New{MissingField}\"");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);
        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();
        // Assert
        Assert.Single(
            memoryDestination.Data,
            (dynamic row) =>
                row.NewId == 2 && row.NewName == "NewTest" && row.NewMissingField == null
        );
    }

    [Fact]
    public void ShouldFailTransformExpandoObjectWithMissingFieldOnSource()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>();
        script.Mappings.Add("NewId", "Id + 1");
        script.Mappings.Add("NewName", "$\"New{Name}\"");
        script.Mappings.Add("NewMissingField", "$\"New{MissingField}\"");
        script.FailOnMissingField = true;
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        var errorDestination = new MemoryDestination<EtlKitError>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);
        script.LinkErrorTo(errorDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();
        errorDestination.Wait();
        // Assert
        Assert.Single(errorDestination.Data);
    }

    [Fact]
    public void ShouldTransformTypedObject()
    {
        // Arrange
        var memorySource = new MemorySource<MyRowType>();
        memorySource.DataAsList.Add(new MyRowType { Id = 1, Name = "Test" });
        var script = new ScriptedRowTransformation<MyRowType, MyRowType>();
        script.Mappings.Add("Id", "Id + 1");
        script.Mappings.Add("Name", "$\"New{Name}\"");
        var memoryDestination = new MemoryDestination<MyRowType>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);
        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();
        // Assert
        Assert.Single(memoryDestination.Data, row => row.Id == 2 && row.Name == "NewTest");
    }

    [Fact]
    public void ShouldNotFailTransformExpandoObjectWithEmptySource()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add([]);
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            FailOnMissingField = false,
        };
        script.Mappings.Add("NewId", "Id + 1");
        script.Mappings.Add("NewName", "$\"New{Name}\"");
        script.Mappings.Add("NewMissingField", "$\"New{MissingField}\"");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();
        // Assert
        Assert.Single(memoryDestination.Data);
    }

    [Fact]
    public void ShouldGetNewId()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            FailOnMissingField = true,
        };
        script.Mappings.Add("NewId", "MassTransit.NewId.NextSequentialGuid()");
        script.Mappings.Add("Id", "Id + 1");

        var assemblyName = "Files/NewId.dll";
        script.AdditionalAssemblyNames = [assemblyName];

        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();
        // Assert
        Assert.Single(memoryDestination.Data);
        var obj = memoryDestination.Data.First() as IDictionary<string, object?>;
        Assert.True(obj["NewId"] is Guid);
    }

    [Fact]
    public void ShouldSerializeToJsonFromProperty()
    {
        // Arrange
        var data = new ExpandoObject() as IDictionary<string, object?>;
        data["Id"] = 1;
        data["Name"] = "test";

        var obj = new ExpandoObject() as IDictionary<string, object?>;
        obj["Data"] = data as ExpandoObject;

        var memorySource = new MemorySource();
        memorySource.DataAsList.Add((ExpandoObject)obj);

        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            FailOnMissingField = true,
        };
        script.Mappings.Add("Id", "Data.Id + 1");
        script.Mappings.Add("Json", "Newtonsoft.Json.JsonConvert.SerializeObject(Data)");

        script.AdditionalAssemblyNames = ["Newtonsoft.Json.dll"];

        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();
        // Assert
        Assert.Single(memoryDestination.Data);
        var res = memoryDestination.Data.First() as IDictionary<string, object?>;
        Assert.Equal(2, res["Id"]);
        Assert.Equal("{\"Id\":1,\"Name\":\"test\"}", res["Json"]);
    }

    [Fact]
    public void PassThrough_Dynamic_PreservesInputFieldsNotInMappings()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            PassThrough = true,
        };
        script.Mappings.Add("NewId", "Id + 1");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert — both original fields and the new mapped field are present
        Assert.Single(memoryDestination.Data);
        var row = (IDictionary<string, object?>)memoryDestination.Data.First();
        Assert.Equal(1, row["Id"]);
        Assert.Equal("Test", row["Name"]);
        Assert.Equal(2, row["NewId"]);
    }

    [Fact]
    public void PassThrough_Dynamic_MappingOverridesExistingField()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            PassThrough = true,
        };
        script.Mappings.Add("Id", "Id + 100");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert — Id is overridden by mapping, Name is preserved from pass-through
        Assert.Single(memoryDestination.Data);
        var row = (IDictionary<string, object?>)memoryDestination.Data.First();
        Assert.Equal(101, row["Id"]);
        Assert.Equal("Test", row["Name"]);
    }

    [Fact]
    public void PassThrough_False_Dynamic_OutputContainsOnlyMappedFields()
    {
        // Arrange — PassThrough defaults to false
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>();
        script.Mappings.Add("NewId", "Id + 1");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert — only the mapped field is present; originals are not passed through
        Assert.Single(memoryDestination.Data);
        var row = (IDictionary<string, object?>)memoryDestination.Data.First();
        Assert.True(row.ContainsKey("NewId"));
        Assert.False(row.ContainsKey("Id"));
        Assert.False(row.ContainsKey("Name"));
    }

    [Fact]
    public void PassThrough_Typed_PreservesInputPropertiesNotInMappings()
    {
        // Arrange
        var memorySource = new MemorySource<MyRowType>();
        memorySource.DataAsList.Add(new MyRowType { Id = 5, Name = "Alice" });
        var script = new ScriptedRowTransformation<MyRowType, MyRowType> { PassThrough = true };
        script.Mappings.Add("Id", "Id * 2");
        var memoryDestination = new MemoryDestination<MyRowType>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert — Name is preserved from pass-through; Id is overridden by the mapping
        Assert.Single(memoryDestination.Data);
        var row = memoryDestination.Data.First();
        Assert.Equal(10, row.Id);
        Assert.Equal("Alice", row.Name);
    }

    [Fact]
    public void PassThrough_False_Typed_PropertiesNotInMappingsAreDefault()
    {
        // Arrange
        var memorySource = new MemorySource<MyRowType>();
        memorySource.DataAsList.Add(new MyRowType { Id = 5, Name = "Alice" });
        var script = new ScriptedRowTransformation<MyRowType, MyRowType>();
        script.Mappings.Add("Id", "Id * 2");
        var memoryDestination = new MemoryDestination<MyRowType>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert — Name is absent from Mappings and not passed through, so it stays at default (null)
        Assert.Single(memoryDestination.Data);
        var row = memoryDestination.Data.First();
        Assert.Equal(10, row.Id);
        Assert.Null(row.Name);
    }

    [Fact]
    public void PassThrough_Typed_IncompatibleTypes_ThrowsInvalidOperationException()
    {
        // Arrange — TInput (MyRowType) is not assignable to TOutput (MyOtherRowType)
        var memorySource = new MemorySource<MyRowType>();
        memorySource.DataAsList.Add(new MyRowType { Id = 3, Name = "Bob" });
        var script = new ScriptedRowTransformation<MyRowType, MyOtherRowType>
        {
            PassThrough = true,
        };
        script.Mappings.Add("Value", "Id * 10");
        var memoryDestination = new MemoryDestination<MyOtherRowType>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act & Assert — InvalidOperationException propagates as AggregateException from Wait()
        Assert.Throws<AggregateException>(() =>
        {
            memorySource.Execute(CancellationToken.None);
            memoryDestination.Wait();
        });
    }

    [Fact]
    public void PassThrough_Typed_DerivedInput_CopiesBaseProperties()
    {
        // Arrange — MyDerivedRowType extends MyRowType, which extends MyOutputBaseType
        var memorySource = new MemorySource<MyDerivedRowType>();
        memorySource.DataAsList.Add(
            new MyDerivedRowType
            {
                Id = 7,
                Name = "Charlie",
                Extra = "ignored",
            }
        );
        var script = new ScriptedRowTransformation<MyDerivedRowType, MyRowType>
        {
            PassThrough = true,
        };
        script.Mappings.Add("Id", "Id * 3");
        var memoryDestination = new MemoryDestination<MyRowType>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert — base properties are copied; mapping overrides Id; Extra is not on TOutput
        Assert.Single(memoryDestination.Data);
        var row = memoryDestination.Data.First();
        Assert.Equal(21, row.Id);
        Assert.Equal("Charlie", row.Name);
    }

    [Fact]
    public void ShouldLoadSystemAssemblyByName_Dynamic()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            FailOnMissingField = true,
        };
        script.Mappings.Add("Json", "System.Text.Json.JsonSerializer.Serialize(Id)");
        script.AdditionalAssemblyNames = ["System.Text.Json"];
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert
        Assert.Single(memoryDestination.Data);
        var row = (IDictionary<string, object?>)memoryDestination.Data.First();
        Assert.Equal("1", row["Json"]);
    }

    [Fact]
    public void ShouldLoadSystemAssemblyByName_Typed()
    {
        // Arrange — AdditionalAssemblyNames only, no AdditionalImports: verifies WithReferences works in typed path
        var memorySource = new MemorySource<MyRowType>();
        memorySource.DataAsList.Add(new MyRowType { Id = 7, Name = "x" });
        var script = new ScriptedRowTransformation<MyRowType, MyRowType>
        {
            FailOnMissingField = true,
        };
        script.Mappings.Add("Name", "System.Text.Json.JsonSerializer.Serialize(Name)");
        script.AdditionalAssemblyNames = ["System.Text.Json"];
        var memoryDestination = new MemoryDestination<MyRowType>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert
        Assert.Single(memoryDestination.Data);
        Assert.Equal("\"x\"", memoryDestination.Data.First().Name);
    }

    [Fact]
    public void ShouldApplyAdditionalImports_Dynamic()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            FailOnMissingField = true,
        };
        script.Mappings.Add("Json", "JsonSerializer.Serialize(Id)");
        script.AdditionalAssemblyNames = ["System.Text.Json"];
        script.AdditionalImports = ["System.Text.Json"];
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert
        Assert.Single(memoryDestination.Data);
        var row = (IDictionary<string, object?>)memoryDestination.Data.First();
        Assert.Equal("1", row["Json"]);
    }

    [Fact]
    public void ShouldApplyAdditionalImports_Typed()
    {
        // Arrange
        var memorySource = new MemorySource<MyRowType>();
        memorySource.DataAsList.Add(new MyRowType { Id = 7, Name = "world" });
        var script = new ScriptedRowTransformation<MyRowType, MyRowType>
        {
            FailOnMissingField = true,
        };
        script.Mappings.Add("Id", "Id * 2");
        script.Mappings.Add("Name", "JsonSerializer.Serialize(Name)");
        script.AdditionalAssemblyNames = ["System.Text.Json"];
        script.AdditionalImports = ["System.Text.Json"];
        var memoryDestination = new MemoryDestination<MyRowType>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert
        Assert.Single(memoryDestination.Data);
        var row = memoryDestination.Data.First();
        Assert.Equal(14, row.Id);
        Assert.Equal("\"world\"", row.Name);
    }

    [Fact]
    public async Task TypedScriptBuilder_WithImports_AppliesNamespace()
    {
        // Arrange
        var expando = new ExpandoObject() as IDictionary<string, object?>;
        expando["Id"] = 1;
        var assembly = Assembly.Load("System.Text.Json");
        var builder = ScriptBuilder
            .Default.ForType(expando)
            .WithReferences([assembly])
            .WithImports(["System.Text.Json"]);

        // Act
        var runner = builder.CreateRunner("JsonSerializer.Serialize(Id)");
        var diagnostics = runner.Script.Compile();
        var result = (await runner.RunAsync(expando)).ReturnValue;

        // Assert
        Assert.Empty(diagnostics);
        Assert.Equal("1", result?.ToString());
    }

    [Fact]
    public void NullableContextOptions_DefaultIsDisable()
    {
        // Arrange & Act
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>();

        // Assert — default preserves backward compatibility.
        Assert.Equal(NullableContextOptions.Disable, script.NullableContextOptions);
    }

    [Fact]
    public void NullableContextOptions_Disable_RejectsNullableAnnotationSyntax()
    {
        // Arrange. A nullable annotation produces CS8632 when the script is compiled
        // with nullable disabled; any diagnostic blocks the runner and routes to errors.
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>();
        script.Mappings.Add("Result", "((string?)Name) ?? \"fallback\"");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        var errorDestination = new MemoryDestination<EtlKitError>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);
        script.LinkErrorTo(errorDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();
        errorDestination.Wait();

        // Assert
        Assert.Single(errorDestination.Data);
    }

    [Fact]
    public void NullableContextOptions_Enable_AllowsNullableAnnotationSyntax_Dynamic()
    {
        // Arrange
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            NullableContextOptions = NullableContextOptions.Enable,
        };
        script.Mappings.Add("Result", "((string?)Name) ?? \"fallback\"");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert
        Assert.Single(memoryDestination.Data);
        var row = (IDictionary<string, object?>)memoryDestination.Data.First();
        Assert.Equal("Test", row["Result"]);
    }

    [Fact]
    public void NullableContextOptions_Enable_AllowsNullableAnnotationSyntax_Typed()
    {
        // Arrange
        var memorySource = new MemorySource<MyRowType>();
        memorySource.DataAsList.Add(new MyRowType { Id = 1, Name = "Test" });
        var script = new ScriptedRowTransformation<MyRowType, MyRowType>
        {
            NullableContextOptions = NullableContextOptions.Enable,
        };
        script.Mappings.Add("Name", "((string?)Name) ?? \"fallback\"");
        var memoryDestination = new MemoryDestination<MyRowType>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert
        Assert.Single(memoryDestination.Data, row => row.Name == "Test");
    }

    [Fact]
    public void NullableContextOptions_Enable_AcceptsUserDefinedNullableDirective()
    {
        // Arrange — user already starts their script with `#nullable enable`.
        // Our injection prepends another directive; Roslyn collapses consecutive
        // same-state directives, so compilation must succeed.
        var memorySource = new MemorySource();
        memorySource.DataAsList.Add(CreateTestDataItem(1, "Test"));
        var script = new ScriptedRowTransformation<ExpandoObject, ExpandoObject>
        {
            NullableContextOptions = NullableContextOptions.Enable,
        };
        script.Mappings.Add("Result", "#nullable enable\n((string?)Name) ?? \"fallback\"");
        var memoryDestination = new MemoryDestination<ExpandoObject>();
        memorySource.LinkTo(script);
        script.LinkTo(memoryDestination);

        // Act
        memorySource.Execute(CancellationToken.None);
        memoryDestination.Wait();

        // Assert
        Assert.Single(memoryDestination.Data);
        var row = (IDictionary<string, object?>)memoryDestination.Data.First();
        Assert.Equal("Test", row["Result"]);
    }

    private static ExpandoObject CreateTestDataItem(int id, string name)
    {
        var result = new ExpandoObject();
        result.TryAdd("Id", id);
        result.TryAdd("Name", name);
        return result;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class MyRowType
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class MyOtherRowType
    {
        public int Value { get; set; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class MyDerivedRowType : MyRowType
    {
        public string? Extra { get; set; }
    }
}
