# A simple data flow

The main part of EtlKit is the Data Flow library. It basically is the ETL part, and holds all
components for extracting, transforming and loading data. All Data Flow components reside in the
'EtlKit.DataFlow' namespace.

## What is a data flow?

You have some data somewhere - stored in some files or a database table or a web service. Now you
want to define a pipeline which takes this data, transforms it "on the fly" and writes it into a
target (this could be again a database, a file or anywhere else). This is the pure essence of an ETL
process (extracting, transforming, loading). The building block to define such a data flow in EtlKit
are source components for extracting, transformations for modifications and destination components
for loading.

### Source components

All dataflow pipelines will need at least one or more sources. Sources are basically everything that
can read data from someplace (e.g. CSV file or database table) and then post this data into the
pipeline. All sources are able to read data asynchronously. That means, while the component reads
data from the source, it simultaneously sends the already processed data to components that are
connected to source. This is crucial when you operate with big amounts of data - you want be able to
process chunks of sources and avoid to load your whole source data into memory first. There are be
some exceptions to this behaviour, depending on your transformation type.

There are different build-in data sources in EtlKit, e.g.: `CsvSource`, `DbSource` or `ExcelSource`
that can be easily use to connect to your data. If you are in need of another source component, you
can extend the `CustomSource`.

Once a source starts reading data, it will start sending data to its connected components. These
could be either a Transformation or Destination. Posting data is always done asynchronously in
separate threads, even if you use the blocking Execute() method on the source to wait for all data
to arrive.

### Transformations

Transformations always have at least one input and one output. Inputs can be connected either to
other transformations or sources, and the output can connect to other transformations or to
destinations. The purpose of a transformation component is to take the data from its input(s) and
post the transformed data to its output(s). This is done on a row-by-row basis for non-blocking
transformation, some batches stored in memory for partially blocking transformations or on the
complete set of data for blocking transformations. Every transfomation has some buffer for it's
input(s) and output(s) in order to improve performance. As soon as there is any data in the input,
non-blocking transformation will start processing and post the result to the output.

### Destination components

Destination components will have normally only one input. They define a target for your data, e.g. a
database table, a file or a .NET collection. If you are in need of another destination component,
you can extend the `CustomDestination`.

Every Destination comes with an input buffer. While a Destination for csv target will open a file
stream where data is written into it as soon as arrives, a DB target will do this batch-by-batch -
therefore, it will wait until the input buffer reaches the batch size (or the data is the last
batch) and then insert it into the database using a bulk insert.

## A simple dataflow

Let's look at a simple dataflow like this:

Csv File (Source) --> Row transformation --> Database destination.

### Source file

The input file "demodata.csv" has the following content:

| rownr | name      | quantity_m | quantity_l | price_in_cents |
| ----- | --------- | ---------- | ---------- | -------------- |
| 1     | "T-Shirt" | 5          | 0          | 1200           |
| 2     | "Shirt"   | 3          | 7          | 1500           |
| 3     | "Jeans"   | 5          | 2          | 3000           |

It should simulate orders for clothing. All data is separated with a ",", and the first row is the
header.

### Destination table

We want to store the data from the file in a destination table in Sql Server. The destination table
will look like this:

| ColumnName | Data Type     | Contraints                   |
| ---------- | ------------- | ---------------------------- |
| Id         | INT           | PK,Identity (auto increment) |
| Item       | NVARCHAR(50)  |                              |
| Quantity   | INT           |                              |
| Price      | DECIMAL(10,2) |                              |

### Creating the components

First we need to create a source, in this example it contain the order data. This will look like
this:

```csharp
CsvSource<string[]> source = new CsvSource<string[]>("demodata.csv");
```

We now add a row transformation. The output format of the `CsvSource` is a string array. In this
example, we will convert the csv string array into an `Order` object and add some logic in the
transformation.

```csharp
RowTransformation<string[], Order> rowTrans = new RowTransformation<string[], Order>(
    row => new Order()
    {
        Item = row[1],
        Quantity = int.Parse(row[2]) + int.Parse(row[3]),
        Price = int.Parse(row[4]) / 100
    });
```

_Please note that you don't have to necessarily use a string array for reading csv file. You can use
the CsvSource already with the right data object._

Now we need to create a destination. For the database destination we need a connection manager that
stores the connection string and provides support for a native ADO.NET connection to the database.

```csharp
SqlConnectionManager connMan = new SqlConnectionManager("Data Source=.;Initial Catalog=demo;Integrated Security=false;User=sa;password=reallyStrongPwd123");
```

Always use the right connection manager for you database - e.g., the SqlConnectionManager will
connect with a Sql Server (and expects a sql server connection string). There are also other
connection managers (e.g. `SQLiteConnectionManager` for SQLite, `PostgresConnectionManager` for
Postgres or `MySqlConnectionManager` for MySql).

_If you don't want to pass the connection manager object over and over again to your DataFlow or
ControlFlow objects, you can store a default connection in the static property
`ControlFlow.DefaultDbConnection`._

No we need a database destination.

```csharp
DbDestination<Order> dest = new DbDestination<Order>(connMan, "OrderTable");
```

Notice that the destination is typed with the `Order` object. We also need to pass the connection
manager to the DbDestination so that connection to our Sql Server can be used, and we provide the
table name for the destination table.

The Order object is a POCO (Plain Old CLR Object) and looks like this:

```csharp
public class Order
{
   public string Item { get; set; }
   public int Quantity { get; set; }
   public double Price { get; set; }
}
```

### Linking the components

Until now we have only created the components, but we didn't define the Data Flow pipe. Let's do
this now:

```csharp
source.LinkTo(rowTrans);
rowTrans.LinkTo(dest);
```

This will create a data flow pipe CsvSource -> RowTransformation -> DbDestination. If your data flow
becomes more complex, linking will become an essential part of your creation. There are
transformations that have more than one input or output, and destinations accepts data from several
sources. Even sources can split data across their destinations - you can defines rules how to split
data as so called predicates.

### Executing the dataflow

Now we will give the source the command to start reading data.

```csharp
  source.Execute();
```

This code will execute as an synchronous task - though the data flow itself will run in it's own
thread. This method will continue execution when all data was read from the source and posted into
the data flow. This does not mean that your data has arrived at the destination yet - but reading
from the source was done successfully when this method returns. To operate totally asynchrounously,
you can use the `ExecuteAsync()` method.

Now we want to wait for the Data Flow pipeline to finish. So we add this line to our code

```csharp
dest.Wait();
```

When `dest.Wait()` returns, all data was read from the source and written into the database table.
To operate totally asynchrounously, you can use the `Completion` property to receive a Task object
for further handling.

```csharp
await dest.Completion;
```

If you are new to the .NET Task parallel library (TPL) and asynchronous programming, I recommend
to use the `Execute()` & `Wait()` pattern to run your data flows. If you want to use `ExecuteAsync()`
and `Completion`, learn more about
[Asynchronous programming with async and await here.](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/)
