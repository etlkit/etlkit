# EtlKit logo EtlKit

A lightweight ETL (extract, transform, load) library and data integration toolbox for .NET. Source
and destination components let you read and write data from the most common databases and file
types. Transformations allow you to you harmonize, filter, aggregate, validate and clean your data.

Create your own tailor-made data flow with your .NET language of choice. EtlKit is written in C# and
offers full support for .NET Core.

## Why EtlKit

EtlKit is a comprehensive C# class library that is able to manage your whole
[ETL](https://en.wikipedia.org/wiki/Extract,_transform,_load) or
[ELT](https://en.wikipedia.org/wiki/Extract,_load,_transform). You can use it to create your own
dataflow pipelines programmatically in .NET, e.g. with C#. Besides a big set of dataflow components
it comes which some control flow task that let you easily manage your database or simple execute Sql
code without any boilerplate code. It also offers extended logging capabilities based on
Microsoft.Extensions.Logging to monitor and analyze your ETL job runs.

EtlKit is a fully functional alternative to other ETL tools like Sql Server Integrations Services
(SSIS). Creating your ETL processes programatically has some advantages:

**Build ETL in .NET**: Code your ETL with your favorite .NET language fitting your team's skills and
that is coming with a mature toolset.

**Runs everywhere**: EtlKit runs on Linux, macOS, and Windows. It is written in the current .NET
Standard and successfully tested with the latest versions of .NET Core & .NET.

**Run locally**: Develop and test your ETL code locally on your desktop using your existing
development & debugging tools.

**Process In-Memory**: EtlKit comes with dataflow components that allow in-memory processing which
is much faster than storing data on disk and processing later.

**Manage Change**: Track you changes with git (or other source controls), code review your etl
logic, and use your existing CI/CD processes.

**Data integration**: While supporting different databases, flat files and web services, you can use
EtlKit as a foundation for your custom made Data Integregation platform.

**Made for big data**: EtlKit relies on
[Microsoft's TPL.Dataflow library](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library)
and was designed to work with big amounts of data.

## Data Flow and Control Flow

EtlKit is split into two main components: Data Flow and Control Flow Tasks. The Data Flow part
offers the core ETL components. The tasks in the Control Flow allow you to manage your databases
with a simple syntax. Both components come with customizable logging functionalities.

### Data Flow overview

EtlKit comes with a set of Data Flow component to construct your own ETL pipeline . You can connect
with different sources (e.g. a Csv file), add some transformations to manipulate that data
on-the-fly (e.g. calculating a sum or combining two columns) and then store the changed data in a
connected destination (e.g. a database table).

To create your own data flow , you basically follow three steps:

- First you define your dataflow components (sources, optionally transformations and destinations)
- link these components together
- tell your source to start reading the data and wait for the destination to finish

Now the source will start reading and post its data into the components connected to its output. As
soon as a connected component retrieves any data in its input, the component will start with
processing the data and then send it further down the line to its connected components. The dataflow
will finish when all data from the source(s) are read, processed by the transformations and received
in the destination(s).

Transformations are not always needed - you can directly connect a source to a destination.
Normally, each source has one output, each destination one input and each transformation at least
one input and one or more outputs.

Of course, all data is processed asynchronously by the components. Each compoment has its own set of
buffers, so while the source is still reading data, the transformations can already process it and
the destinations can start writing the processed information into their target. So in an optimal
flow only the current row needed for processing is stored in memory. Depending on the processing
speed of your components, the buffer of each component can store additional rows to optimize
throughput.

#### Data Flow example

It's easy to create your own data flow pipeline. This example data flow will read data from a MySql
database, modify a value and then store the modified data in a Sql Server table and a csv file,
depending on a filter expression.

Step 1 is to create a source, the transformations and destinations:

```csharp
var sourceCon = new MySqlConnectionManager("Server=10.37.128.2;Database=EtlKit_ControlFlow;Uid=etlkit;Pwd=etlkitpassword;");
var destCon = new SqlConnectionManager("Data Source=.;Integrated Security=SSPI;Initial Catalog=EtlKit;");

DbSource<MySimpleRow> source = new DbSource<MySimpleRow>(sourceCon, "SourceTable");
RowTransformation<MySimpleRow, MySimpleRow> rowTrans = new RowTransformation<MySimpleRow, MySimpleRow>(
    row => {
        row.Value += 1;
        return row;
    });
Multicast<MySimpleRow> multicast = new Multicast<MySimpleRow>();
DbDestination<MySimpleRow> sqlDest = new DbDestination<MySimpleRow>(destCon, "DestinationTable");
CsvDestination<MySimpleRow> csvDest = new CsvDestination<MySimpleRow>("example.csv");
```

Now we link these elements together.

```csharp
source.LinkTo(trans);
rowTrans.LinkTo(multicast);
multicast.LinkTo(sqlDest, row => row.FilterValue > 0);
multicast.LinkTo(csvDest, row => row.FilterValue < 0);
```

Finally, start the dataflow at the source and wait for the destinations to rececive all data (and
the completion message from the source).

```csharp
source.Execute();
sqlDest.Wait();
csvDest.Wait();
```

### Data integration

The heart of an ETL framework is it's ability to integrate with other systems. The following table
shows which types of sources and destination are supported out-of-the box with the current version
of EtlKit. **You can _always_ integrate any other system not listed here by using a `CustomSource`
or `CustomDestination` - though you have to write the integration code yourself.**

| Source or Destination | Support for                          | Limitations                                   |
| --------------------- | ------------------------------------ | --------------------------------------------- |
| Databases             | Sql Server, Postgres, SQLite, MySql  | Full support                                  |
| Flat files            | Csv, Json, Xml                       | Full support                                  |
| Office                | Microsoft Access, Excel              | Full support for Access, Excel only as source |
| Cube                  | Sql Server Analysis Service          | Only XMLA statements                          |
| Memory                | .NET IEnumerable & Collections       | Full support                                  |
| Cloud Services        | Tested with Azure                    | Full support                                  |
| Any other             | integration with custom written code | No limitations                                |

You can choose between different sources and destination components. `DbSource` and `DbDestination`
will connect to the most used databases (e.g. Sql Server, Postgres, MySql, SQLite). `CsvSource`,
`CsvDestination` give you support for flat files -
[based on CSVHelper](https://joshclose.github.io/CsvHelper/). `ExcelSource` allows you to read data
from an excel sheet. `JsonSource`, `JsonDestination`, `XmlSource` and `XmlDestination` let you read
and write json from files or web service request. `MemorySource`, `MemoryDestinatiation` as well as
`CustomSource` and `CustomDestination` will give you a lot flexibility to read or write data
directly from memory or to create your own custom made source or destination component.

### Transformations

EtlKit has 3 type of transformations: Non-blocking, partially blocking and blocking transformations.
Non-blocking transformations will only store the row that is currently processed in memory (plus
some more in the buffer to optimize throughput and performance). Partially blocking transformations
will load some data in the memory before they process data row-by-row. Blocking transformations will
wait until all data has arrived at the component before it starts processing all records
subsequently.

The following table is an overview of the most common transformations in EtlKit:

| Non-blocking      | Partially blocking   | Blocking            |
| ----------------- | -------------------- | ------------------- |
| RowTransformation | LookupTransformation | BlockTransformation |
| Aggregation       | CrossJoin            | Sort                |
| MergeJoin         |                      |                     |
| Multicast         |                      |                     |
| RowDuplication    |                      |                     |
| RowMultiplication |                      |                     |

#### Designed for big data

EtlKit was designed for performance and is able to deal with big amounts of data. All destinations
do support Bulk or Batch operations. By default, every component comes with an input and/or output
buffer. You can design your data flow that only batches or your data is stored in memory, which are
kept in different buffers for every component to increase throughput. All operations can be execute
asynchrounously, so that your processing will run only within separate threads.

### Control Flow - overview

Control Flow Tasks gives you control over your database: They allow you to create or delete
databases, tables, procedures, schemas or other objects in your database. With these tasks you also
can truncate your tables, count rows or execute _any_ sql you like. Anything you can script in sql
can be done here - but with only one line of easy-to-read C# code. This improves the readability of
your code a lot, and gives you more time to focus on your business logic.

Code tells - here is some example code, without writing the whole "boilerplate" code by ADO.NET.

```csharp
var conn = new SqlConnectionManager("Server=10.37.128.2;Database=EtlKit_ControlFlow;Uid=etlkit;Pwd=etlkitpassword;");
//Execute some Sql
SqlTask.ExecuteNonQuery(conn, "Do some sql",$@"EXEC myProc");
//Count rows
int count = RowCountTask.Count(conn, "demo.table1").Value;
//Create a table (works on all databases)
CreateTableTask.Create(conn, "Table1", new List<TableColumn>() {
    new TableColumn(name:"key",dataType:"INT",allowNulls:false,isPrimaryKey:true, isIdentity:true),
    new TableColumn(name:"value", dataType:"NVARCHAR(100)",allowNulls:true)
});
```

### Logging

EtlKit logs through [Microsoft.Extensions.Logging](https://learn.microsoft.com/dotnet/core/extensions/logging),
so you can use any logging provider you already know (console, file, Serilog, and so on). By default
nothing is logged; to activate logging you assign a configured `ILoggerFactory` to the static
`ControlFlow.LoggerFactory` property:

```csharp
ControlFlow.LoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
```

After that you get logging output for all tasks and components in EtlKit. EtlKit can also write log
entries into a database table via the optional `EtlKit.Logging.Database` package. See the
[logging documentation](controlflow/logging.md) for details.

## Getting EtlKit

You can use EtlKit within any .NET or .NET core project that supports .NET Standard 2.0. (Basically
all latest versions of .NET)

**Variant 1:** Nuget

[EtlKit is available on nuget](https://www.nuget.org/packages/EtlKit). Just add the package to your
project via your nuget package manager.

**Variant 2:** Download the sources

Clone the repository:

```bash
git clone https://github.com/etlkit/etlkit.git
```

Then, open the downloaded solution file EtlKit.sln with Visual Studio 2019 or higher. Now you can
build the solution, and use it as a reference in other projects.

## Going further

EtlKit is open source. Feel free to make changes or to fix bugs. Every particiation in this open
source project is appreciated.

To dig deeper into it, have a look at the test projects. There is a test for (almost) everything
that you can do with EtlKit.
