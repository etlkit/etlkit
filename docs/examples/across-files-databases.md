# Example: Across files and databases

## Purpose

Sometimes you are in need to transfer data across databases on different server or to integrate flat
files. EtlKit is an excellent tool of choice for integrating data from different sources. This
example will guide you through the most common scenarios.

### Prerequisites

The example code will not go into the details of the sql code to create the necessary table or
files. It will focus on how to move the data from the source to the destinations. If you are
interest how you can create the tables or databases with EtlKit, have a look at the
[ControlFlow tasks](../controlflow/overview.md)

## Import or exporting CSV files

The easiest (and yet very powerful) example is to import or export data from a database table
from/into a csv file.

Let's start with the import of a csv file.

### CSV Import

First, we need to create a connection manager which describe the connection to our database. In this
example, we will connect to a Postgres database - but this will also work with any other supported
database.

```csharp
string PostgresConnectionString = @"Server=.;Database=demodataflow;User Id=postgres;Password=etlkitpassword;";
PostgresConnectionManager conMan = new PostgresConnectionManager(PostgresConnectionString);
```

No we need to create the components for the CSV source and the database destination.

```csharp
//Import CSV
CsvSource sourceCSV = new CsvSource("NameList.csv");
DbDestination importDest = new DbDestination(conMan, "NameTable");
```

No we link the components together and execute the data flow.

```csharp
sourceCSV.LinkTo(importDest);

sourceCSV.Execute();
importDest.Wait();
```

In our demo we start the data flow with `Execute()` - this will read all data from the source and
post it into the dataflow. While reading data, incoming batches will already be written into the
destination. With the `Wait()` method we wait for all data to be written into the target. The
program won't continue until the flow has finished (though most of the data processing will be done
in a separate task). If you know how to deal with the Tasks Parallel library, you can use
`ExecuteAsync()` and `Completion()` instead.

This example only works if you have a csv file named `NameList.csv` copied into your output
directory. It should look like this:

```csharp
LastName,FirstName
Bunny,Bugs
,Tweety
Devil,Tasmanian
Duck,Daffy
Sylvester,
Porky Pig
Yosemite,Sam
Fudd,Elmer
```

Finally, here is the code to create the necessary table on your database of choice. I used the
`CreateTableTask` which offers a database neutral way to create a table (this will work on
SqlServer, Postgres, MySql, SQLite ...) But of course you can always use regular Sql or other
frameworks like Entitiy Framework...

```csharp
List<TableColumn> tc = new List<TableColumn>()
{
    new TableColumn("Id","INTEGER",allowNulls:false, isPrimaryKey:true, isIdentity:true),
    new TableColumn("FirstName", "VARCHAR(500)", allowNulls: true),
    new TableColumn("LastName", "VARCHAR(500)", allowNulls: true),
};
CreateTableTask.Create(conMan, "NameTable", tc);
```

**Note**: Please note that the table created above has an identity column (or serial called in
Postgres, or auto increment in MySql). This column has an integer value starting with 1, that auto
increments for every inserted entry.

Of course, execute the create table code before you run the dataflow...

Running this example will import the csv file into the Postgres table. It should look like this:

| Id  | FirstName | LastName  |
| --- | --------- | --------- |
| 1   | Bunny     | Bugs      |
| 2   | Tweety    |           |
| 3   | Devil     | Tasmanian |
| 4   | Duck      | Daffy     |
| 6   | Porky Pig | _NULL_    |
| 7   | Yosemite  | Sam       |
| 8   | Fudd      | Elmer     |

### Export CSV

No we want to export the data again, which is just the other way round.

But to spice this example a little bit up, we now add an object type that hold the data during the
processing. You don't need to specify the object type as shown above - in this case, EtlKit will use
a string array instead. But if you are in the need to work with your data, e.g. by add a
transformation in the flow, it is easier if you have defined a class that specifies your data type.

```csharp
public class NameListElement
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
```

No we create the components for the export, link them together and start the data flow.

```csharp
DbSource<NameListElement> sourceTable = new DbSource<NameListElement>(conMan, "NameTable");
CsvDestination<NameListElement> destCSV = new CsvDestination<NameListElement>("Export.csv");
destCSV.Configuration.Delimiter = ";";
sourceTable.LinkTo(destCSV);
sourceTable.Execute();
destCSV.Wait();
```

You will find a file called `Export.csv` in your output directory. As you have perhaps noted, we
changed the default csv separator from "," to ";" by changing the configuration. The csv will look
like this:

```csharp
Id;FirstName;LastName
1;Bunny;Bugs
2;;Tweety
3;Devil;Tasmanian
4;Duck;Daffy
5;Sylvester;
6;Porky Pig;
7;Yosemite;Sam
8;Fudd;Elmer
```

**Note**: As we create a table with an Identity column, our object also now contains an `Id`
property. If we would want to export the data without Id, just remove the property from the object.

## Transfer across databases

Sometimes you just want to transfer data across different database. Let's say we want to take the
data in the table `NameTable` (which we just used for our csv import) and transfer it into a Sql
Server database. (It could have be any other database, or another Postgres database on another
server).

And to make it a little bit more complicated, the destination table on Sql Server looks a little bit
different - it will also have an Id column (again as an Identity), but instead of FirstName and
LastName it does only have a FullName column which is obviously a combination of first and last
name.

First let's create the destination table on Sql Server with a CreateTableTask (or do this manually):
To do so, we will also need a new connection manager that can connect with Sql Server:

```csharp
string SqlServerConnectionString = @"Data Source=.;Initial Catalog=EtlKit_DataFlow;Integrated Security=false;User=sa;password=reallyStrongPwd123";
SqlConnectionManager conMan = new SqlConnectionManager(SqlServerConnectionString);#

List<TableColumn> tc = new List<TableColumn>()
{
    new TableColumn("Id","INTEGER",allowNulls:false, isPrimaryKey:true, isIdentity:true),
    new TableColumn("FullName", "VARCHAR(1000)", allowNulls: true)
};
CreateTableTask.Create(conMan, "FullNameTable", tc);
```

Now we extend our `NameListElement` by a new Property that will store the FullName:

```csharp
public class NameListElement
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
}
```

With this preparations, we can now create the data flow. This time, we will add a
`RowTransformation` in between the source and the destination.

```csharp
PostgresConnectionManager postgresConMan = new PostgresConnectionManager(PostgresConnectionString);
SqlConnectionManager sqlConMan = new SqlConnectionManager(SqlServerConnectionString);

DbSource<NameListElement> source = new DbSource<NameListElement>(postgresConMan, "NameTable");
RowTransformation<NameListElement> trans = new RowTransformation<NameListElement>(
    row =>
    {
        row.FullName = row.LastName + "," + row.FirstName;
        return row;
    }) ;
DbDestination<NameListElement> dest = new DbDestination<NameListElement>(sqlConMan, "FullNameTable");

//Linking the components
source.LinkTo(trans);
trans.LinkTo(dest);
```

The RowTransformation has a function that concatenates first and last name and writes it into the
property `FullName`. Actually, this transformation wouldn't been necessary for this example -
instead, you could have defined the property with
`public string FullName => LastName + "," + FirstName;` in your `NameListElement` object. But this
way this example demonstrated the power of the RowTransformation a little bit.

Finally, we execute the data flow and wait for the completion.

```csharp
source.Execute();
dest.Wait();
```

The destination table on Sql Server should now look like this:

| Id  | FullName        |
| --- | --------------- |
| 1   | Bugs,Bunny      |
| 2   | Tweety,         |
| 3   | Tasmanian,Devil |
| 4   | Daffy,Duck      |
| 5   | ,Sylvester      |
| 6   | ,Porky Pig      |
| 7   | Sam,Yosemite    |
| 8   | Elmer,Fudd      |

You have successfully transfered table data from a Postgres database into a Sql Server database and
transformed it on-the-fly. This is one simple example of an ETL process (Extract, Transform, Load).
