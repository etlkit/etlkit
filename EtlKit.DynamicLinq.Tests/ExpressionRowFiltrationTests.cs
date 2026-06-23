using System.Dynamic;
using System.Linq.Dynamic.Core;
using EtlKit.DataFlow;
using EtlKit.DynamicLinq;
using EtlKit.Primitives;
using JetBrains.Annotations;

namespace EtlKit.DynamicLinq.Tests;

public class ExpressionRowFiltrationTests
{
    private static ExpandoObject MakeRow(params (string key, object value)[] fields)
    {
        var row = new ExpandoObject();
        IDictionary<string, object?> dict = row;
        foreach (var (key, value) in fields)
            dict[key] = value;
        return row;
    }

    private static List<ExpandoObject> RunFiltration(string expression, params ExpandoObject[] rows)
    {
        var source = new MemorySource<ExpandoObject>();
        foreach (var row in rows)
            source.DataAsList.Add(row);

        var filtration = new ExpressionRowFiltration(expression);
        var dest = new MemoryDestination<ExpandoObject>();

        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        source.Execute();
        dest.Wait();

        return dest.Data.ToList();
    }

    // --- Simple field comparison ---

    [Fact]
    public void SimpleFieldComparison_NotEquals()
    {
        var row1 = MakeRow(("AdminReserveRatio", 25), ("AdminReserveRatioToday", 30));
        var row2 = MakeRow(("AdminReserveRatio", 25), ("AdminReserveRatioToday", 25));

        var result = RunFiltration("AdminReserveRatio != AdminReserveRatioToday", row1, row2);

        Assert.Single(result); // row2 filtered out (25 == 25)
    }

    [Fact]
    public void SimpleFieldComparison_Equals()
    {
        var row1 = MakeRow(("Type", "Day"));
        var row2 = MakeRow(("Type", "Period"));

        var result = RunFiltration("Type == \"Day\"", row1, row2);

        Assert.Single(result);
        Assert.Equal("Day", ((IDictionary<string, object?>)result[0])["Type"]);
    }

    // --- Compare with constant ---

    [Fact]
    public void CompareWithConstant_GreaterThan()
    {
        var row1 = MakeRow(("Reserve", 100m));
        var row2 = MakeRow(("Reserve", 0m));
        var row3 = MakeRow(("Reserve", -50m));

        var result = RunFiltration("Reserve > 0", row1, row2, row3);

        Assert.Single(result); // only row1
    }

    // --- Logical AND ---

    [Fact]
    public void LogicalAnd()
    {
        var row1 = MakeRow(("Reserve", 100m), ("Type", "Day"));
        var row2 = MakeRow(("Reserve", 100m), ("Type", "Period"));
        var row3 = MakeRow(("Reserve", 0m), ("Type", "Day"));

        var result = RunFiltration("Reserve > 0 && Type == \"Day\"", row1, row2, row3);

        Assert.Single(result); // only row1
    }

    // --- Logical OR ---

    [Fact]
    public void LogicalOr()
    {
        var row1 = MakeRow(("Type", "Day"));
        var row2 = MakeRow(("Type", "Period"));
        var row3 = MakeRow(("Type", "ChangeRatio"));

        var result = RunFiltration("Type == \"Day\" || Type == \"Period\"", row1, row2, row3);

        Assert.Equal(2, result.Count); // row1 and row2
    }

    // --- Logical NOT ---

    [Fact]
    public void LogicalNot()
    {
        var row1 = MakeRow(("Reserve", 0m));
        var row2 = MakeRow(("Reserve", 100m));

        var result = RunFiltration("!(Reserve == 0)", row1, row2);

        Assert.Single(result); // only row2
    }

    // --- Nested logic with parentheses ---

    [Fact]
    public void NestedLogicWithParentheses()
    {
        var row1 = MakeRow(("A", 10), ("B", 50), ("C", "Day"));
        var row2 = MakeRow(("A", 10), ("B", 150), ("C", "Day"));
        var row3 = MakeRow(("A", -1), ("B", 50), ("C", "Period"));

        var result = RunFiltration("(A > 0 && B < 100) || C == \"Period\"", row1, row2, row3);

        Assert.Equal(2, result.Count); // row1 and row3
    }

    // --- Arithmetic in expression ---

    [Fact]
    public void ArithmeticExpression()
    {
        var row1 = MakeRow(("AccrualByDaySum", 100m), ("BurnByDaySum", 10m));
        var row2 = MakeRow(("AccrualByDaySum", 50m), ("BurnByDaySum", 50m));
        var row3 = MakeRow(("AccrualByDaySum", 10m), ("BurnByDaySum", 100m));

        var result = RunFiltration("AccrualByDaySum - BurnByDaySum > 0", row1, row2, row3);

        Assert.Single(result); // only row1 (90 > 0)
    }

    // --- Complex arithmetic ---

    [Fact]
    public void ComplexArithmetic()
    {
        var row1 = MakeRow(
            ("AccrualByDaySum", 200m),
            ("BurnByDaySum", 10m),
            ("AdminReserveRatio", 25)
        );
        var row2 = MakeRow(
            ("AccrualByDaySum", 10m),
            ("BurnByDaySum", 200m),
            ("AdminReserveRatio", 25)
        );

        var result = RunFiltration(
            "(AccrualByDaySum - BurnByDaySum) * AdminReserveRatio / 100 > 0",
            row1,
            row2
        );

        Assert.Single(result); // only row1
    }

    // --- Null check ---

    [Fact]
    public void NullCheck()
    {
        var row1 = MakeRow(("AuthLimit", 1000m));
        var row2 = MakeRow(("AuthLimit", null!));

        var result = RunFiltration("AuthLimit != null", row1, row2);

        Assert.Single(result); // only row1
    }

    // --- Complex combined expression ---

    [Fact]
    public void ComplexCombinedExpression()
    {
        var row1 = MakeRow(
            ("AdminReserveRatio", 25),
            ("AdminReserveRatioToday", 30),
            ("Reserve", 100m),
            ("Type", "Day")
        );
        var row2 = MakeRow(
            ("AdminReserveRatio", 25),
            ("AdminReserveRatioToday", 25),
            ("Reserve", 100m),
            ("Type", "Day")
        );
        var row3 = MakeRow(
            ("AdminReserveRatio", 25),
            ("AdminReserveRatioToday", 30),
            ("Reserve", 0m),
            ("Type", "Day")
        );
        var row4 = MakeRow(
            ("AdminReserveRatio", 25),
            ("AdminReserveRatioToday", 30),
            ("Reserve", 100m),
            ("Type", "Recalculation")
        );

        var result = RunFiltration(
            "AdminReserveRatio != AdminReserveRatioToday && Reserve > 0 && Type != \"Recalculation\"",
            row1,
            row2,
            row3,
            row4
        );

        Assert.Single(result); // only row1
    }

    // --- TransformExpression ---

    [Fact]
    public void DebugDynamicClassFactory_CheckPropertyTypes()
    {
        var dict = new Dictionary<string, object>
        {
            ["Reserve"] = 100m,
            ["Name"] = "Test",
            ["Count"] = 42,
        };
        var properties = dict.Select(p => new DynamicProperty(p.Key, p.Value.GetType())).ToList();
        var type = DynamicClassFactory.CreateType(properties);

        // Verify typed properties
        var reserveProp = type.GetProperty("Reserve");
        Assert.NotNull(reserveProp);
        Assert.Equal(typeof(decimal), reserveProp.PropertyType);

        var countProp = type.GetProperty("Count");
        Assert.NotNull(countProp);
        Assert.Equal(typeof(int), countProp.PropertyType);

        // Create and populate instance
        var instance = Activator.CreateInstance(type)!;
        foreach (var p in dict)
            type.GetProperty(p.Key)!.SetValue(instance, p.Value);

        // Create typed array via reflection so AsQueryable sees the DynamicClass type
        var typedArray = Array.CreateInstance(type, 1);
        typedArray.SetValue(instance, 0);
        var query = typedArray.AsQueryable();

        var result = query.Where("Reserve > 0").Any();
        Assert.True(result);
    }

    // --- Empty FilterExpression ---

    [Fact]
    public void EmptyFilterExpression_Throws()
    {
        var row = MakeRow(("A", 1));

        Assert.Throws<AggregateException>(() => RunFiltration("", row));
    }

    // --- All rows pass ---

    [Fact]
    public void AllRowsPass()
    {
        var row1 = MakeRow(("Value", 1));
        var row2 = MakeRow(("Value", 2));

        var result = RunFiltration("Value > 0", row1, row2);

        Assert.Equal(2, result.Count);
    }

    // --- No rows pass ---

    [Fact]
    public void NoRowsPass()
    {
        var row1 = MakeRow(("Value", -1));
        var row2 = MakeRow(("Value", -2));

        var result = RunFiltration("Value > 0", row1, row2);

        Assert.Empty(result);
    }

    // --- Package variable substitution (simulated) ---

    [Fact]
    public void PackageVariableSubstitution()
    {
        // $(LoyaltyProgramId) would be substituted by ETL runtime to "1" before parsing
        // Simulate by passing the already-substituted expression
        var row1 = MakeRow(("LoyaltyProgramId", 1));
        var row2 = MakeRow(("LoyaltyProgramId", 2));

        var result = RunFiltration("LoyaltyProgramId == 1", row1, row2);

        Assert.Single(result);
    }

    // --- Nested ExpandoObject: null check on nested member ---

    [Fact]
    public void NestedExpando_NullCheckOnMember()
    {
        var withDate = new ExpandoObject();
        ((IDictionary<string, object?>)withDate)["ReportDate"] = new DateTime(
            2026,
            4,
            25,
            0,
            0,
            0,
            DateTimeKind.Unspecified
        );
        var row1 = MakeRow(("Context", withDate));

        var withoutDate = new ExpandoObject();
        ((IDictionary<string, object?>)withoutDate)["ReportDate"] = null!;
        var row2 = MakeRow(("Context", withoutDate));

        var result = RunFiltration("Context.ReportDate != null", row1, row2);

        Assert.Single(result);
    }

    // --- Nested ExpandoObject: numeric comparison on nested member ---

    [Fact]
    public void NestedExpando_NumericComparisonOnMember()
    {
        var order1 = new ExpandoObject();
        ((IDictionary<string, object?>)order1)["Total"] = 150m;
        var row1 = MakeRow(("Order", order1));

        var order2 = new ExpandoObject();
        ((IDictionary<string, object?>)order2)["Total"] = 50m;
        var row2 = MakeRow(("Order", order2));

        var result = RunFiltration("Order.Total > 100", row1, row2);

        Assert.Single(result);
    }

    // --- Deeply nested ExpandoObject ---

    [Fact]
    public void DeeplyNestedExpando_MemberAccess()
    {
        ExpandoObject Build(string cityName)
        {
            var city = new ExpandoObject();
            ((IDictionary<string, object?>)city)["Name"] = cityName;
            var address = new ExpandoObject();
            ((IDictionary<string, object?>)address)["City"] = city;
            var owner = new ExpandoObject();
            ((IDictionary<string, object?>)owner)["Address"] = address;
            return owner;
        }

        var row1 = MakeRow(("Owner", Build("Moscow")));
        var row2 = MakeRow(("Owner", Build("Tver")));

        var result = RunFiltration("Owner.Address.City.Name == \"Moscow\"", row1, row2);

        Assert.Single(result);
    }

    // --- Custom class as field value ---

    [Fact]
    public void CustomClass_PropertyAccess()
    {
        var row1 = MakeRow(("Person", new TestPerson { Name = "John", Age = 30 }));
        var row2 = MakeRow(("Person", new TestPerson { Name = "Jane", Age = 25 }));

        var result = RunFiltration("Person.Name == \"John\" && Person.Age > 18", row1, row2);

        Assert.Single(result);
    }

    private sealed class TestPerson
    {
        public string Name
        {
            [UsedImplicitly]
            get;
            set;
        } = "";
        public int Age
        {
            [UsedImplicitly]
            get;
            set;
        }
    }

    // --- Generic ExpressionRowFiltration<TInput> over typed POCO ---

    private sealed class ChangeRatioRow
    {
        public decimal AdminReserveRatio { get; set; }
        public decimal AdminReserveRatioPrevious { get; set; }
    }

    [Fact]
    public void TypedPoco_FilterByPropertyChange()
    {
        var rows = new[]
        {
            new ChangeRatioRow { AdminReserveRatio = 25, AdminReserveRatioPrevious = 25 }, // dropped
            new ChangeRatioRow { AdminReserveRatio = 25, AdminReserveRatioPrevious = 30 }, // passes
            new ChangeRatioRow { AdminReserveRatio = 30, AdminReserveRatioPrevious = 25 }, // passes
        };

        var source = new MemorySource<ChangeRatioRow>();
        foreach (var row in rows)
            source.DataAsList.Add(row);

        var filtration = new ExpressionRowFiltration<ChangeRatioRow>(
            "AdminReserveRatioPrevious != AdminReserveRatio"
        );
        var dest = new MemoryDestination<ChangeRatioRow>();

        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        source.Execute();
        dest.Wait();

        Assert.Equal(2, dest.Data.Count);
        Assert.All(
            dest.Data,
            row => Assert.NotEqual(row.AdminReserveRatio, row.AdminReserveRatioPrevious)
        );
    }

    [Fact]
    public void TypedPoco_FilterByArithmetic()
    {
        var rows = new[]
        {
            new ChangeRatioRow { AdminReserveRatio = 25, AdminReserveRatioPrevious = 10 }, // 25 - 10 = 15 > 0 -> passes
            new ChangeRatioRow { AdminReserveRatio = 5, AdminReserveRatioPrevious = 30 }, // 5 - 30 = -25 -> dropped
        };

        var source = new MemorySource<ChangeRatioRow>();
        foreach (var row in rows)
            source.DataAsList.Add(row);

        var filtration = new ExpressionRowFiltration<ChangeRatioRow>(
            "AdminReserveRatio - AdminReserveRatioPrevious > 0"
        );
        var dest = new MemoryDestination<ChangeRatioRow>();

        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        source.Execute();
        dest.Wait();

        Assert.Single(dest.Data);
        Assert.Equal(25, dest.Data.First().AdminReserveRatio);
    }

    // --- Collection of nested dicts: Any(predicate) ---

    [Fact]
    public void CollectionOfDictionaries_AnyPredicate()
    {
        ExpandoObject MakeItem(decimal sum)
        {
            var item = new ExpandoObject();
            ((IDictionary<string, object?>)item)["Sum"] = sum;
            return item;
        }

        var row1 = MakeRow(("Items", new object[] { MakeItem(50m), MakeItem(150m) }));
        var row2 = MakeRow(("Items", new object[] { MakeItem(50m), MakeItem(70m) }));

        var result = RunFiltration("Items.Any(Sum > 100)", row1, row2);

        Assert.Single(result);
    }

    // --- Collection: Count() ---

    [Fact]
    public void CollectionOfDictionaries_Count()
    {
        ExpandoObject MakeItem(int id)
        {
            var item = new ExpandoObject();
            ((IDictionary<string, object?>)item)["Id"] = id;
            return item;
        }

        var rowMany = MakeRow(("Lines", new object[] { MakeItem(1), MakeItem(2), MakeItem(3) }));
        var rowFew = MakeRow(("Lines", new object[] { MakeItem(1) }));

        var result = RunFiltration("Lines.Count() > 2", rowMany, rowFew);

        Assert.Single(result);
    }

    // --- Collection: Sum(selector) ---

    [Fact]
    public void CollectionOfDictionaries_SumSelector()
    {
        ExpandoObject MakeItem(decimal amount)
        {
            var item = new ExpandoObject();
            ((IDictionary<string, object?>)item)["Amount"] = amount;
            return item;
        }

        var rowOver = MakeRow(("Lines", new object[] { MakeItem(60m), MakeItem(50m) }));
        var rowUnder = MakeRow(("Lines", new object[] { MakeItem(10m), MakeItem(20m) }));

        var result = RunFiltration("Lines.Sum(Amount) > 100", rowOver, rowUnder);

        Assert.Single(result);
    }

    // --- Scalar collection: Contains ---

    [Fact]
    public void ScalarCollection_Contains()
    {
        var rowPremium = MakeRow(("Tags", new[] { "Basic", "Premium", "VIP" }));
        var rowBasic = MakeRow(("Tags", new[] { "Basic", "Standard" }));

        var result = RunFiltration("Tags.Contains(\"Premium\")", rowPremium, rowBasic);

        Assert.Single(result);
    }

    // --- Empty collection: Count() works, Any(predicate) does not ---

    [Fact]
    public void EmptyCollection_CountReturnsZero()
    {
        var row = MakeRow(("Items", Array.Empty<object>()));

        var result = RunFiltration("Items.Count() == 0", row);

        Assert.Single(result);
    }

    // --- Heterogeneous collection: unification rules ---

    [Fact]
    public void HeterogeneousCollection_DifferentFieldSets_FieldsUnifiedAsNullable()
    {
        // Items differ in which fields they carry. The unified element shape
        // contains both A and B; fields missing from a given item project to null
        // (Nullable<int>) rather than default(int)=0, so predicates do not see
        // a phantom 0 value for absent fields.
        var item1 = new ExpandoObject();
        ((IDictionary<string, object?>)item1)["A"] = 1; // B absent
        var item2 = new ExpandoObject();
        ((IDictionary<string, object?>)item2)["A"] = 2;
        ((IDictionary<string, object?>)item2)["B"] = 5;

        var row = MakeRow(("Items", new object[] { item1, item2 }));

        // Both elements are mapped (no throw on heterogeneous shape).
        Assert.Single(RunFiltration("Items.Count() == 2", row));

        // A is present in both items, accessible via predicate.
        Assert.Single(RunFiltration("Items.Any(A == 1)", row));
        Assert.Single(RunFiltration("Items.Any(A == 2)", row));

        // B is present only in item2; item1's missing B must NOT show as 0.
        Assert.Single(RunFiltration("Items.Any(B == 5)", row));
        Assert.Empty(RunFiltration("Items.Any(B == 0)", row));
    }

    [Fact]
    public void HeterogeneousCollection_NullVsNonNullValueType_FieldUnifiedAsNullable()
    {
        // The motivating case for note 84547: optional value-type fields where
        // some items have null and others a concrete value. Field type widens to
        // Nullable<T> automatically.
        var item1 = new ExpandoObject();
        ((IDictionary<string, object?>)item1)["X"] = null!;
        var item2 = new ExpandoObject();
        ((IDictionary<string, object?>)item2)["X"] = 100m;

        var row = MakeRow(("Items", new object[] { item1, item2 }));

        var result = RunFiltration("Items.Any(X > 50)", row);
        Assert.Single(result);
    }

    [Fact]
    public void HeterogeneousCollection_ConflictingNonNullTypes_Throws()
    {
        // Genuine type conflict in the same field across items - no safe
        // unification, throws with a clear pointer to the conflicting field.
        var item1 = new ExpandoObject();
        ((IDictionary<string, object?>)item1)["A"] = 1;
        var item2 = new ExpandoObject();
        ((IDictionary<string, object?>)item2)["A"] = "string";

        var row = MakeRow(("Items", new object[] { item1, item2 }));

        Assert.Throws<AggregateException>(() => RunFiltration("Items.Count() > 0", row));
    }

    [Fact]
    public void HeterogeneousCollection_DictAndScalarMix_Throws()
    {
        // Mix of dictionary item and a scalar item is a real shape mismatch -
        // no unified projection makes sense.
        var item1 = new ExpandoObject();
        ((IDictionary<string, object?>)item1)["A"] = 1;

        var row = MakeRow(("Items", new object[] { item1, "not-a-dict" }));

        Assert.Throws<AggregateException>(() => RunFiltration("Items.Count() > 0", row));
    }

    // --- Malformed expression: parse error propagates ---

    [Fact]
    public void MalformedExpression_WithoutErrorHandler_Throws()
    {
        var row = MakeRow(("Reserve", 100m));

        Assert.Throws<AggregateException>(() => RunFiltration("Reserve >>> 0", row));
    }

    [Fact]
    public void MalformedExpression_WithErrorHandler_GoesToErrorBuffer()
    {
        var source = new MemorySource<ExpandoObject>();
        source.DataAsList.Add(MakeRow(("Reserve", 100m)));

        var filtration = new ExpressionRowFiltration("Reserve >>> 0");
        var dest = new MemoryDestination<ExpandoObject>();
        var errorDest = new MemoryDestination<EtlKitError>();

        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        filtration.LinkErrorTo(errorDest);
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        Assert.Empty(dest.Data);
        Assert.Single(errorDest.Data);
    }

    // --- Unknown field reference goes to error buffer ---

    [Fact]
    public void UnknownField_WithErrorHandler_GoesToErrorBuffer()
    {
        var source = new MemorySource<ExpandoObject>();
        source.DataAsList.Add(MakeRow(("Reserve", 100m)));

        var filtration = new ExpressionRowFiltration("NonExistentField > 0");
        var dest = new MemoryDestination<ExpandoObject>();
        var errorDest = new MemoryDestination<EtlKitError>();

        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        filtration.LinkErrorTo(errorDest);
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        Assert.Empty(dest.Data);
        Assert.Single(errorDest.Data);
    }

    // --- Null row is silently dropped (handled by base RowFiltration) ---

    [Fact]
    public void NullRow_IsSilentlyDropped()
    {
        var source = new MemorySource<ExpandoObject>();
        source.DataAsList.Add(MakeRow(("Reserve", 100m)));
        source.DataAsList.Add(null!);
        source.DataAsList.Add(MakeRow(("Reserve", 200m)));

        var filtration = new ExpressionRowFiltration("Reserve > 0");
        var dest = new MemoryDestination<ExpandoObject>();

        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        source.Execute();
        dest.Wait();

        Assert.Equal(2, dest.Data.Count);
    }

    // --- Type coverage: bool ---

    [Fact]
    public void BoolField_Equality()
    {
        var row1 = MakeRow(("IsActive", true));
        var row2 = MakeRow(("IsActive", false));

        var result = RunFiltration("IsActive == true", row1, row2);

        Assert.Single(result);
    }

    // --- Type coverage: DateTime (compare two DateTime fields in the same row) ---

    [Fact]
    public void DateTimeField_Comparison()
    {
        var row1 = MakeRow(
            ("CreatedAt", new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc)),
            ("Threshold", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        );
        var row2 = MakeRow(
            ("CreatedAt", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            ("Threshold", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        );

        var result = RunFiltration("CreatedAt > Threshold", row1, row2);

        Assert.Single(result);
    }

    // --- Type coverage: Guid ---

    [Fact]
    public void GuidField_Equality()
    {
        var sameGuid = Guid.NewGuid();
        var row1 = MakeRow(("CurrentId", sameGuid), ("ExpectedId", sameGuid));
        var row2 = MakeRow(("CurrentId", Guid.NewGuid()), ("ExpectedId", Guid.NewGuid()));

        var result = RunFiltration("CurrentId == ExpectedId", row1, row2);

        Assert.Single(result);
    }

    // --- Type coverage: byte[] is treated as scalar (not as collection) ---

    [Fact]
    public void ByteArrayField_TreatedAsScalar_NotCollection()
    {
        var row1 = MakeRow(("Data", new byte[] { 1, 2, 3 }));
        var row2 = MakeRow(("Data", null!));

        var result = RunFiltration("Data != null", row1, row2);

        Assert.Single(result);
    }

    // --- Mixed batch: failing row goes to error, valid rows pass ---

    [Fact]
    public void MixedBatch_FailingRowGoesToError_ValidRowsPass()
    {
        var source = new MemorySource<ExpandoObject>();
        source.DataAsList.Add(MakeRow(("Reserve", 100m))); // OK
        source.DataAsList.Add(MakeRow(("OtherField", "x"))); // missing Reserve -> error
        source.DataAsList.Add(MakeRow(("Reserve", 50m))); // OK

        var filtration = new ExpressionRowFiltration("Reserve > 0");
        var dest = new MemoryDestination<ExpandoObject>();
        var errorDest = new MemoryDestination<EtlKitError>();

        source.LinkTo(filtration);
        filtration.LinkTo(dest);
        filtration.LinkErrorTo(errorDest);
        source.Execute();
        dest.Wait();
        errorDest.Wait();

        Assert.Equal(2, dest.Data.Count);
        Assert.Single(errorDest.Data);
    }

    // --- ParsingConfig.CustomTypeProvider lifecycle ---

    private sealed class ProvenanceRow
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public int Foo { get; set; }
    }

    [Fact]
    public void ManuallySetCustomTypeProvider_PreservedWhenNoOurRegistrations()
    {
        // User assigns a CustomTypeProvider directly and never touches our
        // RegisterCustomTypes / AdditionalAssemblyNames / AdditionalImports.
        // RebuildTypeProvider must leave the provider untouched.
        var filtration = new ExpressionRowFiltration<ProvenanceRow>("Foo > 0");
        var manualProvider = new DynamicLinqTypeProvider(new HashSet<Type> { typeof(string) });
        filtration.ParsingConfig.CustomTypeProvider = manualProvider;

        // Trigger compile path so RebuildTypeProvider would run if it was going to.
        Assert.True(filtration.PredicateFunc!(new ProvenanceRow { Foo = 1 }));

        Assert.Same(manualProvider, filtration.ParsingConfig.CustomTypeProvider);
    }

    [Fact]
    public void AdditionalImports_ClearedAfterSet_DropsOurInstalledProvider()
    {
        // After installing our provider via AdditionalImports, clearing the
        // collection must drop the stale provider on the next evaluation rather
        // than leave it pinned.
        var filtration = new ExpressionRowFiltration<ProvenanceRow>("Foo > 0");
        filtration.AdditionalImports = new[] { "Bogus.Namespace" };
        Assert.True(filtration.PredicateFunc!(new ProvenanceRow { Foo = 1 }));

        var providerAfterImport = filtration.ParsingConfig.CustomTypeProvider;
        Assert.IsType<DynamicLinqTypeProvider>(providerAfterImport);

        filtration.AdditionalImports = Array.Empty<string>();
        Assert.True(filtration.PredicateFunc!(new ProvenanceRow { Foo = 1 }));

        Assert.IsNotType<DynamicLinqTypeProvider>(filtration.ParsingConfig.CustomTypeProvider);
    }
}
