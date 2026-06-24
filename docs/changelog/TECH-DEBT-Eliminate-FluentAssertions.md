# Tech Debt: Eliminate FluentAssertions from the Solution

> **Status: COMPLETED** (2026-04-06)

## Summary

Replace all FluentAssertions usage with xUnit's built-in `Assert` class and remove the
`FluentAssertions` NuGet package from the solution. The majority of the test suite already uses xUnit
`Assert` exclusively (~1000 calls across 12 test projects). FluentAssertions is only used in 9 files
across 5 projects (~208 assertion calls), making this a contained migration.

## Motivation

- **Reduce dependencies**: FluentAssertions 6.12.0 is an external dependency that adds maintenance
  overhead (version upgrades, license tracking, transitive dependencies)
- **Consistency**: The codebase already uses xUnit `Assert` as the primary assertion library in the
  vast majority of tests. Having two assertion styles creates confusion about which to use
- **Simplicity**: The FluentAssertions patterns used here are straightforward and map directly to
  xUnit equivalents with no loss of expressiveness

## Current State

### Package References (FluentAssertions 6.12.0)

| Project | File |
|---------|------|
| EtlKit.AI.Tests | `EtlKit.AI.Tests/EtlKit.AI.Tests.csproj` |
| EtlKit.Rest.Tests | `EtlKit.Rest.Tests/EtlKit.Rest.Tests.csproj` |
| EtlKit.Serialization.Tests | `EtlKit.Serialization.Tests/EtlKit.Serialization.Tests.csproj` |
| TestHelper | `TestShared/TestHelper/TestHelper.csproj` |
| TestTransformations | `TestTransformations/TestTransformations.csproj` |

### Files Using FluentAssertions (9 files)

1. `EtlKit.Rest.Tests/RestTransformationTests.cs`
2. `EtlKit.Serialization.Tests/DataFlowTests.cs`
3. `EtlKit.Serialization.Tests/DataFlowXmlReaderDITests.cs`
4. `EtlKit.Serialization.Tests/DefaultDataFlowActivatorTests.cs`
5. `EtlKit.Serialization.Tests/LoggerInjectionTests.cs`
6. `EtlKit.Serialization.Tests/ServiceCollectionExtensionsTests.cs`
7. `EtlKit.Serialization.Tests/ServiceProviderActivatorTests.cs`
8. `EtlKit.Serialization.Tests/TypeExtensionsTests.cs`
9. `TestTransformations/src/SqlQueryTransformation/SqlQueryTransformationTests.cs`

### Assertion Pattern Inventory (~208 total calls)

| FluentAssertions Pattern | Count | xUnit Replacement |
|--------------------------|------:|-------------------|
| `.Should().NotBeNull()` | ~50 | `Assert.NotNull(x)` |
| `.Should().Be(expected)` | ~50 | `Assert.Equal(expected, actual)` |
| `.Should().BeSameAs(expected)` | ~19 | `Assert.Same(expected, actual)` |
| `.Should().BeFalse()` | ~15 | `Assert.False(x)` |
| `.Should().BeNull()` | ~15 | `Assert.Null(x)` |
| `.Should().HaveCount(n)` | ~12 | `Assert.Equal(n, collection.Count)` |
| `.Should().BeTrue()` | ~5 | `Assert.True(x)` |
| `.Should().ContainKey(key)` | ~5 | `Assert.True(dict.ContainsKey(key))` or `Assert.Contains(key, dict.Keys)` |
| `.Should().NotBeEmpty()` | ~3 | `Assert.NotEmpty(collection)` |
| `.Should().ContainEquivalentOf(x)` | ~3 | `Assert.Contains(collection, item => /* match */)` |
| `.Should().NotBeNullOrEmpty()` | ~2 | `Assert.NotNull(x); Assert.NotEmpty(x)` |
| `.Should().Throw<T>().WithParameterName()` | ~2 | `var ex = Assert.Throws<T>(() => ...); Assert.Equal(name, ex.ParamName)` |
| `.Should().BeOfType<T>()` | ~1 | `Assert.IsType<T>(x)` |
| `.Should().BeEquivalentTo(expected)` | ~1 | `Assert.Equal()` or custom comparison |
| `.Should().HaveCountGreaterThan(0)` | ~1 | `Assert.NotEmpty(collection)` |
| `.Should().NotContainKey(key)` | ~1 | `Assert.False(dict.ContainsKey(key))` or `Assert.DoesNotContain(key, dict.Keys)` |

## Migration Plan

### Phase 1: Migrate EtlKit.Serialization.Tests (7 files, largest scope)

This project has the most FluentAssertions usage. Migrate all 7 files:

1. `DataFlowTests.cs`
2. `DataFlowXmlReaderDITests.cs`
3. `DefaultDataFlowActivatorTests.cs`
4. `LoggerInjectionTests.cs`
5. `ServiceCollectionExtensionsTests.cs`
6. `ServiceProviderActivatorTests.cs`
7. `TypeExtensionsTests.cs`

**Steps:**
- Replace `using FluentAssertions;` with xUnit `Assert` calls per the mapping table above
- Remove `<PackageReference Include="FluentAssertions" Version="6.12.0" />` from
  `EtlKit.Serialization.Tests.csproj`
- Run `dotnet test EtlKit.Serialization.Tests/` to verify all tests pass

### Phase 2: Migrate TestTransformations (1 file)

- Migrate `src/SqlQueryTransformation/SqlQueryTransformationTests.cs`
- Remove `<PackageReference Include="FluentAssertions" ... />` from
  `TestTransformations/TestTransformations.csproj`
- Run `dotnet test TestTransformations/` to verify

### Phase 3: Migrate EtlKit.Rest.Tests (1 file)

- Migrate `RestTransformationTests.cs`
- Remove `<PackageReference Include="FluentAssertions" ... />` from
  `EtlKit.Rest.Tests/EtlKit.Rest.Tests.csproj`
- Run `dotnet test EtlKit.Rest.Tests/` to verify

### Phase 4: Clean up remaining references

- Remove `<PackageReference Include="FluentAssertions" ... />` from
  `EtlKit.AI.Tests/EtlKit.AI.Tests.csproj` (package referenced but not yet used in code)
- Remove `<PackageReference Include="FluentAssertions" ... />` from
  `TestShared/TestHelper/TestHelper.csproj` (package referenced but not yet used in code)
- Verify full solution build: `dotnet build EtlKit.sln`
- Run full test suite to confirm no regressions

### Phase 5: Verification

- `dotnet build EtlKit.sln` succeeds with no FluentAssertions references
- Grep the solution for any remaining `FluentAssertions` references:
  `Should()`, `using FluentAssertions`, `PackageReference.*FluentAssertions`
- All tests pass

## Conversion Reference

### Simple value assertions

```csharp
// BEFORE (FluentAssertions)
result.Should().Be(42);
result.Should().NotBe(0);
result.Should().BeTrue();
result.Should().BeFalse();
result.Should().BeNull();
result.Should().NotBeNull();

// AFTER (xUnit Assert)
Assert.Equal(42, result);
Assert.NotEqual(0, result);
Assert.True(result);
Assert.False(result);
Assert.Null(result);
Assert.NotNull(result);
```

### Reference equality

```csharp
// BEFORE
obj.Should().BeSameAs(expected);

// AFTER
Assert.Same(expected, obj);
```

### Collection assertions

```csharp
// BEFORE
list.Should().HaveCount(5);
list.Should().NotBeEmpty();
list.Should().HaveCountGreaterThan(0);
list.Should().ContainEquivalentOf(expectedItem);

// AFTER
Assert.Equal(5, list.Count);
Assert.NotEmpty(list);
Assert.NotEmpty(list);
Assert.Contains(list, item => /* equivalence check */);
```

### Dictionary assertions

```csharp
// BEFORE
dict.Should().ContainKey("key");
dict.Should().NotContainKey("key");

// AFTER
Assert.True(dict.ContainsKey("key"));
Assert.False(dict.ContainsKey("key"));
// or
Assert.Contains("key", (IDictionary<string, T>)dict);
Assert.DoesNotContain("key", (IDictionary<string, T>)dict);
```

### Type assertions

```csharp
// BEFORE
obj.Should().BeOfType<ExpectedType>();

// AFTER
Assert.IsType<ExpectedType>(obj);
```

### Exception assertions

```csharp
// BEFORE
action.Should().Throw<ArgumentNullException>().WithParameterName("param");

// AFTER
var ex = Assert.Throws<ArgumentNullException>(action);
Assert.Equal("param", ex.ParamName);
```

### String assertions

```csharp
// BEFORE
str.Should().NotBeNullOrEmpty();

// AFTER
Assert.NotNull(str);
Assert.NotEmpty(str);
// or simply
Assert.False(string.IsNullOrEmpty(str));
```

### Deep equivalence (BeEquivalentTo)

```csharp
// BEFORE
actual.Should().BeEquivalentTo(expected);

// AFTER - case by case:
// For simple objects with Equal override:
Assert.Equal(expected, actual);
// For collections of primitives:
Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
// For complex objects without Equal override, compare properties explicitly
```

## Effort Estimate

- **Scope**: ~208 assertion calls across 9 files
- **Risk**: Low -- all replacements are mechanical 1:1 mappings
- **Testing**: Each phase is independently verifiable with `dotnet test`
