# Tech Debt: DI-based Activator Mode for DataFlowXmlReader

> **Status: COMPLETED** (2026-04-07) — shipped in 1.16.0. `IDataFlowActivator` plus
> `DefaultDataFlowActivator` and `ServiceProviderActivator` are in
> `EtlKit.Serialization/DataFlow/`. `IServiceCollection` registration extensions
> (`AddEtlKitCore`, `AddEtlKitJson`, `AddEtlKitKafka`, `AddEtlKitRabbitMq`,
> `AddEtlKitRest`, `AddEtlKitScripting`, `AddEtlKitAI`, `AddEtlKitSerialization`) are
> registered per library, and `ILogger<T>` constructor overloads were added to every
> data flow step. See [CHANGELOG.md](../../CHANGELOG.md) entry for 1.16.0.

## Summary

Implement an alternative activation mode for `DataFlowXmlReader` that uses Microsoft Dependency Injection (`IServiceProvider`) instead of the current `DataFlowActivator` approach.

## Current Implementation

- **File**: `EtlKit.Serialization/DataFlow/DataFlowXmlReader.cs`
- **Activator**: `EtlKit.Serialization/DataFlow/DataFlowActivator.cs`

Currently, `DataFlowXmlReader` uses `DataFlowActivator.CreateInstance(type)` to instantiate types during XML deserialization. The `DataFlowActivator` relies on `Activator.CreateInstance()` which only supports parameterless constructors (with special cases for known types like `CsvConfiguration`).

### Affected Code Locations

```csharp
// DataFlowXmlReader.cs:262
var list = DataFlowActivator.CreateInstance(type);

// DataFlowXmlReader.cs:280
var instance = DataFlowActivator.CreateInstance(type);
```

## Proposed Enhancement

Add an optional `IServiceProvider` parameter to `DataFlowXmlReader` that enables DI-based instance creation:

### Benefits

1. **Extensibility**: New data flow steps can have constructor dependencies injected
2. **Service Integration**: Steps can depend on services registered in the DI container (logging, configuration, caching, etc.)
3. **Testability**: Easier to mock dependencies when testing custom steps
4. **Consistency**: Aligns with modern .NET application patterns

### Suggested Implementation Approach

1. **Add optional `IServiceProvider` to `DataFlowXmlReader` constructor**:
   ```csharp
   public DataFlowXmlReader(
       IDataFlow dataFlow,
       CultureInfo? currentCulture = null,
       IDataFlowDestination<EtlKitError>? linkAllErrorsTo = null,
       IServiceProvider? serviceProvider = null  // NEW
   )
   ```

2. **Create `IDataFlowActivator` interface**:
   ```csharp
   public interface IDataFlowActivator
   {
       object? CreateInstance(Type type);
   }
   ```

3. **Implement `ServiceProviderActivator`**:
   ```csharp
   public class ServiceProviderActivator : IDataFlowActivator
   {
       private readonly IServiceProvider _serviceProvider;

       public ServiceProviderActivator(IServiceProvider serviceProvider)
       {
           _serviceProvider = serviceProvider;
       }

       public object? CreateInstance(Type type)
       {
           // First try to resolve from the container to respect registered lifetimes
           // (Transient/Scoped/Singleton). Fall back to ActivatorUtilities.CreateInstance
           // for types not registered in the container.
           return _serviceProvider.GetService(type)
               ?? ActivatorUtilities.CreateInstance(_serviceProvider, type);
       }
   }
   ```

4. **Refactor existing `DataFlowActivator` to implement `IDataFlowActivator`**:
   ```csharp
   public class DefaultDataFlowActivator : IDataFlowActivator
   {
       public object? CreateInstance(Type type) { /* existing logic */ }
   }
   ```

5. **Update `DataFlowXmlReader` to use `IDataFlowActivator`**:
   ```csharp
   private readonly IDataFlowActivator _activator;

   public DataFlowXmlReader(...)
   {
       _activator = serviceProvider != null
           ? new ServiceProviderActivator(serviceProvider)
           : new DefaultDataFlowActivator();
   }
   ```

### Usage Example

```csharp
// Register custom step with dependencies
services.AddTransient<MyCustomSource>();
services.AddSingleton<IMyService, MyService>();

// Create reader with DI support
var serviceProvider = services.BuildServiceProvider();
var reader = new DataFlowXmlReader(dataFlow, serviceProvider: serviceProvider);
```

```csharp
// Custom step with constructor injection
public class MyCustomSource : DataFlowSource<ExpandoObject>
{
    private readonly IMyService _myService;

    public MyCustomSource(IMyService myService)
    {
        _myService = myService;
    }
}
```

## Part 2: Service Collection Registration Extensions

Each EtlKit library should provide extension methods for `IServiceCollection` to register all data flow steps.

### Implementation Plan

1. **Create extension class per library**:
   ```csharp
    // In EtlKit.Core or EtlKit
    public static class EtlKitCoreServiceCollectionExtensions
    {
        public static IServiceCollection AddEtlKitCore(this IServiceCollection services)
       {
           // Register all core data flow components
           services.AddTransient<DbSource<ExpandoObject>>();
           services.AddTransient<DbDestination<ExpandoObject>>();
           services.AddTransient<RowTransformation<ExpandoObject>>();
           // ... all other core steps
           return services;
       }
   }
   ```

2. **Library-specific extensions**:
   ```csharp
    // EtlKit.Csv
    public static class EtlKitCsvServiceCollectionExtensions
    {
        public static IServiceCollection AddEtlKitCsv(this IServiceCollection services)
       {
           services.AddTransient<CsvSource<ExpandoObject>>();
           services.AddTransient<CsvDestination<ExpandoObject>>();
           return services;
       }
   }

    // EtlKit.Json
    public static class EtlKitJsonServiceCollectionExtensions
    {
        public static IServiceCollection AddEtlKitJson(this IServiceCollection services)
       {
           services.AddTransient<JsonSource<ExpandoObject>>();
           services.AddTransient<JsonDestination<ExpandoObject>>();
           return services;
       }
   }

    // EtlKit.Xml
    public static class EtlKitXmlServiceCollectionExtensions
    {
        public static IServiceCollection AddEtlKitXml(this IServiceCollection services)
       {
           services.AddTransient<XmlSource<ExpandoObject>>();
           services.AddTransient<XmlDestination<ExpandoObject>>();
           return services;
       }
   }

    // EtlKit.Serialization
    public static class EtlKitSerializationServiceCollectionExtensions
    {
        public static IServiceCollection AddEtlKitSerialization(this IServiceCollection services)
       {
           services.AddTransient<DataFlowXmlReader>();
           return services;
       }
   }
   ```

3. **Aggregate registration method** *(example for consuming applications, not part of EtlKit packages)*:

   > **Note:** This extension is shown as a reference for third-party consumers who want a single registration call across multiple EtlKit packages. It should NOT be shipped inside any individual EtlKit NuGet package, as it would introduce dependencies on all other EtlKit packages, defeating the purpose of the modular package structure.

   ```csharp
   // Example: consumers can create this in their own project
    public static class EtlKitServiceCollectionExtensions
    {
        public static IServiceCollection AddEtlKit(this IServiceCollection services)
        {
            services.AddEtlKitCore();
            services.AddEtlKitCsv();
            services.AddEtlKitJson();
            services.AddEtlKitXml();
            services.AddEtlKitSerialization();
           // ... other libraries as needed
           return services;
       }
   }
   ```

### Libraries Requiring Registration Extensions

- EtlKit (core)
- EtlKit.Csv
- EtlKit.Json
- EtlKit.Xml
- EtlKit.Excel
- EtlKit.Parquet
- EtlKit.Serialization
- EtlKit.Azure.* (if applicable)
- EtlKit.* (all provider-specific libraries)

## Part 3: ILogger Constructor Support for All Steps

Add an optional `ILogger` parameter to constructors of all data flow steps to enable structured logging. This uses a **combined approach**: base classes store a non-generic `ILogger` property, while concrete classes accept `ILogger<T>` in their constructors for proper log category resolution.

### Design Rationale

- `ILogger<T>` implements `ILogger`, so a concrete class can accept `ILogger<T>` and pass it up to a base class that stores `ILogger` — no information is lost at the DI level, and the base class can use the logger without knowing the concrete type.
- DI containers resolve `ILogger<T>` automatically (no special registration needed), preserving the concrete class name as the log category.
- **Do NOT provide both `ILogger<T>` and `ILogger` constructor overloads on the same class** — this creates DI ambiguity since `ILogger<T>` already is-a `ILogger`. Concrete classes should only accept `ILogger<T>`; base classes should only use non-generic `ILogger`.
- The `= null` default on every constructor level ensures existing parameterless constructors keep working — no breaking change.

### Implementation Pattern

1. **Base class stores `ILogger` and chains through the hierarchy**:

   This replaces the current static `ControlFlow.LoggerFactory.CreateLogger<GenericTask>()` pattern with instance-level logger injection. Each level in the hierarchy forwards the logger via `= null` default parameter.

   ```csharp
   // Top-level base — single storage point for the logger
   public abstract class GenericTask : ITask
   {
       protected ILogger? Logger { get; }

       protected GenericTask(ILogger? logger = null)
       {
           Logger = logger;
       }
   }

   // Intermediate base — forwards logger to GenericTask
   public abstract class DataFlowTask : GenericTask
   {
       protected DataFlowTask(ILogger? logger = null) : base(logger) { }

       // LogStart, LogProgress, LogFinish use this.Logger directly
   }

   // Category-specific base — forwards logger to DataFlowTask
   public abstract class DataFlowSource<TOutput> : DataFlowTask
   {
       protected DataFlowSource(ILogger? logger = null) : base(logger) { }
   }
   ```

2. **Concrete classes accept `ILogger<T>` (generic) for DI resolution**:

   The generic `ILogger<T>` preserves the concrete class name as the log category (e.g., `"EtlKit.DbSource<MyRow>"` appears in log output). Since `ILogger<T> : ILogger`, it flows up to the base naturally.

   ```csharp
   public class DbSource<TOutput> : DataFlowSource<TOutput>
   {
       // Existing parameterless constructor (backward compatibility)
       public DbSource() : this(logger: null) { }

       // Existing constructors chain to the logger-accepting one
       public DbSource(string tableName) : this(logger: null)
       {
           TableName = tableName;
       }

       // New: DI-resolved constructor
       public DbSource(ILogger<DbSource<TOutput>>? logger) : base(logger) { }

       public DbSource(IConnectionManager connectionManager,
                        ILogger<DbSource<TOutput>>? logger = null) : base(logger)
       {
           ConnectionManager = connectionManager;
       }
   }
   ```

3. **Usage in steps — logger is inherited from base, no private field needed**:
   ```csharp
   public class RowTransformation<TInput, TOutput> : DataFlowTransformation<TInput, TOutput>
   {
       public RowTransformation() : this(logger: null) { }

       public RowTransformation(ILogger<RowTransformation<TInput, TOutput>>? logger)
           : base(logger) { }

       protected override void ProcessRow(TInput row)
       {
           Logger?.LogDebug("Processing row: {RowType}", typeof(TInput).Name);
           // ... existing logic — Logger comes from base class
       }
   }
   ```

### Constructor Chaining Depth

The current hierarchy is 4 levels deep (`GenericTask → DataFlowTask → DataFlowSource<T> → DbSource<T>`). Each intermediate level must forward the `ILogger? logger = null` parameter. This is verbose but mechanical and can be done incrementally — one level at a time, bottom-up.

### Affected Step Categories

All data flow components should receive this enhancement:

**Sources**:
- DbSource, CsvSource, JsonSource, XmlSource, ExcelSource, ParquetSource
- CustomSource, MemorySource, etc.

**Transformations**:
- RowTransformation, ColumnTransformation, Aggregation
- Lookup, MergeJoin, CrossJoin, Sort, Distinct
- BatchTransformation, BlockTransformation, etc.

**Destinations**:
- DbDestination, CsvDestination, JsonDestination, XmlDestination
- ExcelDestination, ParquetDestination, CustomDestination, MemoryDestination, etc.

### Logging Integration Points

Consider logging at these points within each step:
- Initialization/configuration
- Start of execution
- Progress (batches processed, rows processed)
- Errors and exceptions
- Completion statistics

## Priority

Medium - This is an enhancement that improves extensibility but doesn't block current functionality.

## Implementation Order

1. Create `IDataFlowActivator` interface and implementations
2. Update `DataFlowXmlReader` to use activator abstraction
3. Add `ILogger? logger = null` parameter to base class hierarchy (`GenericTask` → `DataFlowTask` → intermediate bases)
4. Add `ILogger<T>` constructors to concrete data flow steps (can be done incrementally, per-library)
5. Create `IServiceCollection` registration extensions per library
6. Update documentation and examples

## Related Files

- `EtlKit.Serialization/DataFlow/DataFlowXmlReader.cs`
- `EtlKit.Serialization/DataFlow/DataFlowActivator.cs`
- All source, transformation, and destination classes across EtlKit libraries

## Dependencies

- `Microsoft.Extensions.DependencyInjection.Abstractions` (for `IServiceProvider`, `IServiceCollection`)
- `Microsoft.Extensions.DependencyInjection` (for `ActivatorUtilities`)
- `Microsoft.Extensions.Logging.Abstractions` (for `ILogger`, `ILogger<T>`)
