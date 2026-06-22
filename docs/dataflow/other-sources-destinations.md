# Other sources and destinations

## Custom sources & destinations

EtlKit allows you to write your own implementation of sources and destinations. This gives you a
great flexibility if you need to integrate systems that are currently now included in the list of default
connectors.

### CustomSource

A custom source can generate any type of output you need.
It will accept tow function: One function that generates the your output, and another one that return true if you reached the end of your data.

Let's look at a simple example. Assuming we have a list of strings, and we want to return these string wrapped into an object data for our source.

First we define an object

```csharp
public class MyRow {
    public int Id { get; set; }
    public string Value { get; set; }
}

List<string> Data = new List<string>() { "Test1", "Test2", "Test3" };
int _readIndex = 0;

CustomSource<MySimpleRow> source = new CustomSource<MySimpleRow>(
    () => {
        return new MyRow()
        {
            Id = _readIndex++,
            Value = Data[_readIndex]
        };
    }, 
    () => _readIndex >= Data.Count);
```

CustomSource also works with dynamic ExpandoObject and arrays.

### Custom Destination

The use of a custom destination is even simpler - a custom destination
just calls an action for every received record.

Here is an example:

```csharp
CustomDestination<MySimpleRow> dest = new CustomDestination<MySimpleRow>(
    row => {
        SqlTask.ExecuteNonQuery(Connection, "Insert row",
            $"INSERT INTO dbo.CustomDestination VALUES({row.Id},'{row.Value}')");
    }
);
```

## In-Memory

### Memory Source

A Memory source is a simple source component that accepts a .NET list or enumerable. Use this component
within your dataflow if you already have a collection containing your data available in memory.
When you execute the flow, the memory destination will iterate through the list and
asynchronously post record by record into the flow.

Example code:

```csharp
MemorySource<MySimpleRow> source = new MemorySource<MySimpleRow>();
source.Data = new List<MySimpleRow>()
{
    new MySimpleRow() { Col1 = 1, Col2 = "Test1" },
    new MySimpleRow() { Col1 = 2, Col2 = "Test2" },
    new MySimpleRow() { Col1 = 3, Col2 = "Test3" }
};
```

### MemoryDestination

A memory destination is a component that store the incoming data within a [BlockingCollection](https://docs.microsoft.com/de-de/dotnet/api/system.collections.concurrent.blockingcollection-1?view=netframework-4.8).
You can access the received data within the `Data` property.
Data can be read from this collection as soon as it arrives.

Example:

```csharp
MemoryDestination<MySimpleRow> dest = new MemoryDestination<MySimpleRow>();
// data is accessible in dest.Data
```

When starting the data flow asynchronous, you should wait until the tasks complete. The source task will complete when
all data was posted into the data flow, and the destination task completes when all data has arrived in the destination.
