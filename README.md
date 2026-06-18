# ETLBox.Classic

This project originated as a fully open-source (MIT) fork from ETLBox library by Andreas Lennartz.

Starting with version 2.0 the author of the original library decided to close the source and commercialize its newer branch.
ETLBox.Classic was aiming at keeping original 1.x branch up to date with .Net and DB libraries.

> ⚠️ **IMPORTANT NOTICE:** ⚠️
> 
> ETLBox.Classic went 1000+ commits from being a direct fork of original ETLBox library.
> 
> It was a copy of the original source code, modified to keep it open-source, but over the years multiple new features were added.
> 
> As we are actively developing it, we anticipate the need for breaking changes in the near future.
> In order to avoid versioning conflict with original ETLBox library, we decided to rename version 2.x and on from
> "EtlBox.Classic" to "EtlKit". We expect migration to 2.0 to be as easy as updating Nuget references and changing
> namespaces. Our best guess on the timing of the transition is somewhere in 2025.

## Documentation

- [Guides and tutorials](docs/README.md) — `docs/` contains hand-written guides and tutorials, browsable directly in the repo
- [API Reference](https://rpsft.github.io/etlbox/) — `docfx/` contains the DocFx config that generates the hosted API reference from XML comments

## Installation

You can use EtlBox.Classic within any .NET or .NET core project that supports .NET Standard 2.0. (Basically all latest versions
of .NET)

[EtlBox.Classic is available on nuget](https://www.nuget.org/packages?q=EtlBox.Classic). Just add the package to your project via your
nuget package manager.

See individual package descriptions to make sense of what each of them does.
For very basic set up you would need to install only [EtlBox.Classic](https://www.nuget.org/packages/EtlBox.Classic)

## What is ETLBox

A lightweight ETL (extract, transform, load) library and data integration toolbox for .NET. Source and destination
components let you read and write data from the most common databases and file types. Transformations allow you to you
harmonize, filter, aggregate, validate and clean your data.

Create your own tailor-made data flow with your .NET language of choice. EtlBox.Classic is written in C# and offers full support
for .NET Core.


## Why ETLBox

EtlBox.Classic is a comprehensive C# class library that is able to manage your
whole [ETL](https://en.wikipedia.org/wiki/Extract,_transform,_load)
or [ELT](https://en.wikipedia.org/wiki/Extract,_load,_transform). You can use it to create your own dataflow pipelines
programmatically in .NET, e.g. with C#. Besides a big set of dataflow components it comes which some control flow task
that let you easily manage your database or simple execute Sql code without any boilerplate code. It also offers
extended logging capabilites based on ILogger to monitor and anlayze your ETL job runs.

EtlBox.Classic is a fully functional alternative to other ETL tools like Sql Server Integrations Services (SSIS). Creating your
ETL processes programatically has some advantages:

**Build ETL in .NET**: Code your ETL with your favorite .NET language fitting your team’s skills and that is coming with
a mature toolset.

**Runs everywhere**: EtlBox.Classic runs on Linux, macOS, and Windows. It is written in the current .NET Standard and
successfully tested with the latest versions of .NET Core & .NET.

**Run locally**: Develop and test your ETL code locally on your desktop using your existing development & debugging
tools.

**Process In-Memory**: EtlBox.Classic comes with dataflow components that allow in-memory processing which is much faster than
storing data on disk and processing later.

**Manage Change**: Track you changes with git (or other source controls), code review your etl logic, and use your
existing CI/CD processes.

**Data integration**: While supporting different databases, flat files and web services, you can use EtlBox.Classic as a
foundation for your custom made Data Integregation platform.

**Made for big data**: EtlBox.Classic relies
on [Microsoft's TPL.Dataflow library](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library)
and was designed to work with big amounts of data.

## Data Flow and Control Flow

EtlBox.Classic is split into two main components: Data Flow and Control Flow Tasks. The Data Flow part offers the core ETL
components. The tasks in the Control Flow allow you to manage your databases with a simple syntax. Both components come
with customizable logging functionalities.

### Data Flow overview

EtlBox.Classic comes with a set of Data Flow component to construct your own ETL pipeline . You can connect with different
sources (e.g. a Csv file), add some transformations to manipulate that data on-the-fly (e.g. calculating a sum or
combining two columns) and then store the changed data in a connected destination (e.g. a database table).

To create your own data flow , you basically follow three steps:

- First you define your dataflow components (sources, optionally transformations and destinations)
- link these components together
- tell your source to start reading the data and wait for the destination to finish

Now the source will start reading and post its data into the components connected to its output. As soon as a connected
component retrieves any data in its input, the component will start with processing the data and then send it further
down the line to its connected components. The dataflow will finish when all data from the source(s) are read, processed
by the transformations and received in the destination(s).

Transformations are not always needed - you can directly connect a source to a destination. Normally, each source has
one output, each destination one input and each transformation at least one input and one or more outputs.

Of course, all data is processed asynchronously by the components. Each compoment has its own set of buffers, so while
the source is still reading data, the transformations can already process it and the destinations can start writing the
processed information into their target. So in an optimal flow only the current row needed for processing is stored in
memory. Depending on the processing speed of your components, the buffer of each component can store additional rows to
optimize throughput.

#### Data Flow example

It's easy to create your own data flow pipeline. This example data flow will read data from a MySql database, modify a
value and then store the modified data in a Sql Server table and a csv file, depending on a filter expression.

Step 1 is to create a source, the transformations and destinations:

```C#
var sourceCon = new MySqlConnectionManager("Server=10.37.128.2;Database=ETLBox_ControlFlow;Uid=etlbox;Pwd=etlboxpassword;");
var destCon = new SqlConnectionManager("Data Source=.;Integrated Security=SSPI;Initial Catalog=ETLBox;");

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

```C#
source.LinkTo(trans);
rowTrans.LinkTo(multicast);
multicast.LinkTo(sqlDest, row => row.FilterValue > 0);
multicast.LinkTo(csvDest, row => row.FilterValue < 0);
```

Finally, start the dataflow at the source and wait for the destinations to rececive all data (and the completion message
from the source).

```C#
source.Execute();
sqlDest.Wait();
csvDest.Wait();
```

### Data integration

The heart of an ETL framework is it's ability to integrate with other systems.
The following table shows which types of sources and destination are supported out-of-the box with the current version
of EtlBox.Classic.
**You can *always* integrate any other system not listed here by using a `CustomSource` or `CustomDestination` - though
you have to write the integration code yourself.**

| Source or Destination | Support for                                     | Limitations                                       |
| --------------------- | ----------------------------------------------- | ------------------------------------------------- |
| Databases             | Sql Server, Postgres, SQLite, MySql, Clickhouse | Full support                                      |
| Queues and streaming  | Kafka, RabbitMQ                                 | Kafka — full support, RabbitMQ — destination only |
| Flat files            | Csv, Json, Xml                                  | Full support                                      |
| Office                | Microsoft Access, Excel                         | Full support for Access, Excel only as source     |
| Cube                  | Sql Server Analysis Service                     | Only XMLA statements                              |
| Memory                | .NET IEnumerable & Collections                  | Full support                                      |
| API                   | REST, OpenAI                                    | Full support                                      |
| Cloud Services        | Tested with Azure                               | Full support                                      |
| Any other             | integration with custom written code            | No limitations                                    |

You can choose between different sources and destination components. `DbSource` and `DbDestination` will connect to the
most used databases (e.g. Sql Server, Postgres, MySql, SQLite). `CsvSource`, `CsvDestination` give you support for flat
files - [based on CSVHelper](https://joshclose.github.io/CsvHelper/). `ExcelSource` allows you to read data from an
excel sheet. `JsonSource`, `JsonDestination`, `XmlSource` and `XmlDestination` let you read and write json from files or
web service request. `MemorySource`, `MemoryDestinatiation` as well as `CustomSource` and `CustomDestination` will give
you a lot flexibility to read or write data directly from memory or to create your own custom made source or destination
component.

### Transformations

EtlBox.Classic has 3 type of transformations: Non-blocking, partially blocking and blocking transformations. Non-blocking
transformations will
only store the row that is currently processed in memory (plus some more in the buffer to optimize throughput and
performance). Partially blocking transformations will load some data in the memory before they process data row-by-row.
Blocking transformations will wait until all data has arrived at the component before it starts processing all records
subsequently.

The following table is an overview of the most common transformations in ETLBox:

| Non-blocking              | Partially blocking   | Blocking            |
| ------------------------- | -------------------- | ------------------- |
| RowTransformation         | LookupTransformation | BlockTransformation |
| RowBatchTransformation    |                      |                     |
| Aggregation               | CrossJoin            | Sort                |
| MergeJoin                 |                      |                     |
| Multicast                 |                      |                     |
| RowDuplication            |                      |                     |
| RowMultiplication         |                      |                     |
| RowFiltration             |                      |                     |
| ExpressionRowFiltration   |                      |                     |
| JsonTransformation        |                      |                     |
| ScriptedRowTransformation |                      |                     |
| AIBatchTransformation     |                      |                     |

#### Designed for big data

EtlBox.Classic was designed for performance and is able to deal with big amounts of data. All destinations do support Bulk or
Batch operations. By default, every component comes with an input and/or output buffer. You can design your data flow
that only batches or your data is stored in memory, which are kept in different buffers for every component to increase
throughput. All operations can be execute asynchrounously, so that your processing will run only within separate
threads.

#### Data transformations

Data transformations take input data from source, perform an external operation (via DB, API, or queue) and return
produced result into a destination. Namely data transformations are:

| Transformation            | Input                                                          | Processing                                         | Output                                                                                  |
| ------------------------- | -------------------------------------------------------------- | -------------------------------------------------- | --------------------------------------------------------------------------------------- |
| SqlQueryTransformation    | Parameters to a liquid-based SQL query template                | Execute SQL query                                  | SQL query results (0..N for each input row)                                             |
| SqlCommmandTransformation | Parameters to a liquid-based SQL query template                | Execute SQL non-query statement                    | Input object (or this can be customised with Transform delegate) — 1 for each input row |
| KafkaTransformation       | Parameters to liquid-based message and key templates           | Produce messages to Kafka topic (optional key)     | Input object or null                                                                    |
| RabbitMqTransformation    | Parameters to a liquid-based string message template           | Publish messages to RabbitMQ channel               | Input object or null                                                                    |
| JsonTransformation        | Json object                                                    | Execute JSON path transformation for each field    | Output object where each field is the result of Json path evaluation                    |
| RestTransformation        | Parameters to a liquid-based request URL and body templates    | Execute HTTP request                               | ExpandoObject with response code, raw body and Json parsed body in fields               |
| ScriptRowTransformation   | Object as Globals to C# script templates for each result field | Execute C# code                                    | Result object with one field per script                                                 |
| AIBatchTransformation     | Parameters to a liquid-based AI prompt template                | Execute request to configured OpenAI HTTP endpoint | ExpandoObject with response code, raw body and Json parsed body in fields               |

### Control Flow - overview

Control Flow Tasks gives you control over your database: They allow you to create or delete databases, tables,
procedures, schemas or other objects in your database. With these tasks you also can truncate your tables, count rows or
execute *any* sql you like. Anything you can script in sql can be done here - but with only one line of easy-to-read C#
code. This improves the readability of your code a lot, and gives you more time to focus on your business logic.

Code tells - here is some example code, without writing the whole "boilerplate" code by ADO.NET.

```C#
var conn = new SqlConnectionManager("Server=10.37.128.2;Database=ETLBox_ControlFlow;Uid=etlbox;Pwd=etlboxpassword;");
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

EtlBox.Classic uses [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/)
which is a de-facto standart for modern .Net. EtlBox.Classic also has its own logging-to-database capabilites,
implemented in `EtlBox.Classic.Logging.Database` package, which has its own configuration and uses NLog internally
at the time being.

## Contribution

Clone the repository:

```bash
git clone https://github.com/rpsft/etlbox.git
```

Then, open the downloaded solution file ETLBox.sln with Visual Studio 2022 or Jetbrains Rider.
Now you can build the solution, and use it as a reference in other projects.

### Running tests

See [TEST_SETUP.md](TEST-SETUP.md) for instructions.

## Going further

ETLBox is open source. Feel free to make changes or to fix bugs. Every particiation in this open source project is
appreciated.

To dig deeper into it, have a look at the test projects. There is a test for (almost) everything that you can do with
ETLBox.