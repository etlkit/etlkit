# Working with data types

## Generic approach

Almost all components in EtlKit can be defined with a POCO (Plain old component object), which is a very simple
object describing your data and data types. This object can be used to store your data in your data flow.

Almost all sources provide a column name for every data column. In a CSV file, you nomrally have a header at the top row
with names for each column. In a database table, there is always a column name as well as a data tyep defined.

If you define an object in C#, the name of the column in the source must be mapped to the right property in your object.
By default, the mapping of column names to properties is resolved by the property name itself. E.g. a column named Value1
would stored in the property with the same name. If you use the `ColumnMap` attribute, you can add what column name will be mapped
to the property. If there is no match, the column will be ignored.

Usage example:

```csharp
//Prepare table
SqlTask.ExecuteNonQuery(@"CREATE TABLE demotable (
  Value1 INT NULL,
  Value2 VARCHAR(100) NULL
 )");

public class MySimpleRow {
    public int Value1 { get; set; }
    [ColumnMap("Value2")]
    public string Col2 { get; set; }
}

DbSource<MySimpleRow> source = new DbSource<MySimpleRow>("demotable");
```

The table demotable has 2 column: Value1 with a INT data type and Value2 with an VARCHAR data type. The POCO `MySimpleRow`
has two properties: Value1 and Value2 with a `ColumnMap("Value2")` attribute. The property Value1 is automatically mapped
to the table column Value1 because of their matching names. The table column Value2 is matched to the property Col2 because
of the ColumnMap attribute.

### Ignored columns

If you use a POCO to describe you data type, this object can have a different amount of properties. In our example above,
we could define a POCO that contains an additional property (Let's call it AnotherValue) and leave out Col2. Our object
would look like this:

```csharp
//Prepare table
SqlTask.ExecuteNonQuery(@"CREATE TABLE demotable (
  Value1 INT NULL,
  Value2 VARCHAR(100) NULL
 )");

public class MyNewRow {
    public int Value1 { get; set; }
    public string AnotherValue { get; set }
}
DbSource<MyNewRow> source = new DbSource<MyNewRow>("demotable");
```

If we would use this object to map with our source table, there would be only data read from Value1. Because the property
AnotherValue doesn't have a match in the source, no data will be read from this column.

### Working with Sql statements

If you don't pass a table name but a Sql statement to read data from a database table, the column name is derived from the statement.

Let's look at this example:

```csharp
//Prepare table
SqlTask.ExecuteNonQuery(@"CREATE TABLE demotable (
  Value1 INT NULL,
  Value2 VARCHAR(100) NULL
 )");

public class MyNewRow {
    public int Value1 { get; set; }
    public string AnotherValue { get; set }
}
DbSource<MyNewRow> source = new DbSource<MyNewRow>() {
    Sql = "SELECT Value1, Value2 AS AnotherValue FROM demotable"
};
```

The Sql statement would produce 2 columns: Column 1 with the column name Value1 and column 2 with the column name "AnotherValue".
EtlKit now is able to map the first column to the property Value1 and the second column to the property AnotherValue.
Of course, you still could use the `ColumnMap` Attribute.

### ColumnMap attribute

The `ColumnMap` attribute is used whenever data is read from a database source or written into a database destination. When reading from
a database source, it will tell the reader which database column name is mapped to the property. It will then write the data into the property via
the setter - method. When writing into a database destination, the attribute will tell in which database column the property data is written into.
Here the getter - method is used to get data from the property.

For example, if you have a property `Key`, and you add the `ColumnMap` Attribute to it:

```csharp
[ColumnMap("Id")]
public string Key { 
    get {
        return _key;
    set {
        _key = value.ToString();
    }

public int _key;
```

If you use this object within a `DbSource`, it will read the data from the database column "Id" and then call the `ToString` method on every record
before actually writing it into the property.

Now you could add another property:

```csharp
[ColumnMap("Hash")]
public string HashValue => HashHelper.Encrypt_Char40(this.Key);
```

When you write into a database table that has a column named "Hash", the column mappings will map the result of the HashValue -property
to this column. Every record is then stored in the table as an encrypted hash value of the property "Key".

You can use this mapping behavior for some basic data type transformations. If transformations become more complex, you should have a look at
the [existing transformations](transformations.md) to modify your data.

### Automatic data type conversion

Whenever you read data from any source (database, csv, json, ...) or you write into any destination, sometimes the data types
in your object will be different than the ones your database table or your object.
EtlKit will always try to convert the data into the right type: E.g. when you read data from a CSV file, by default the data
comes as a string. But if your object defines a numeric data type like int or double, it will be automatically converted.
Or if you write into a database table, there could be a DECIMAL column in your table, and your object could hold a string value.
As long as the string in your object can be converted into a decimal value, EtlKit will automatically try to cast your data
into the right type.

## Dynamic object approach

Sometimes you don't want (or can) create an object during design-time for your data flow components. #
You want the properties (and perhaps methods etc.) created during run-time. With .NET and .NET Core,
there you can have dynamic objects, which basically means that you can define object where
no type checks are executed when you compile you program. The keyword here is `dynamic`.

### ExpandoObject

EtlKit offers support for dynamic objects, and relies on the `ExpandoObject`. The ExpandoObject can be cast into
a dynamic, and after the cast properties can be created by simple assigned them a value.

Here is a simple example of the `ExpandoObject`

```csharp
dynamic sampleObject = new ExpandoObject();
sampleObject.test = "Dynamic Property";
///Sample object now has a property "test" of type string with the value "Dynamic Property"
```

[The Microsoft documentation gives you a good explanation of the possibilites of the ExpandoObject and also more details about
the use of `dynamic`.](https://docs.microsoft.com/en-us/dotnet/api/system.dynamic.expandoobject?view=netframework-4.8)

### EtlKit support for ExpandoObject

In order to use the ExpandoObject and dynmic objects with EtlKit, you simple type your data flow with this object.
Alternatively, you just use the non generic object - which automitically will use the ExpandoObject.
The following two lines will do the same:

```csharp
DbSource source = new DbSource("sourceTable");
```

and

```csharp
DbSource<ExpandoObject> source = new DbSource<ExpandoObject>("sourceTable");
```

Let's walk through an example. Assuming we have two tables.
The table `sourceTable` has two columns: SourceCol1 and SourceCol2, both integer values.
The table `destTable` has one column: DestColSum, also an integer value.

We could now define the following data flow:

```csharp
DbSource source = new DbSource("sourceTable");

//Act
RowTransformation trans = new RowTransformation(
    sourcedata =>
    {
        dynamic c = sourcedata as ExpandoObject;
        c.DestColSum = c.SourceCol1 + c.SourceCol2;
        return c;
    });
DbDestination dest = new DbDestination("destTable");
```

In this example code, the data is read from a DbSource into an ExpandoObject. The properties SourceCol1 and SourceCol2
are created automatically, because EtlKit will recognize that it is an ExpandoObject and add a property
for each column in the source.
In the RowTransformation, you can convert the ExpandoObject into a dynamic object first, so that you don't get any errros
message when you compile your code. Now we can assign a new property to the (same) ExpandoObject - in this case, it's called
DestColSum as a sum of the properties SourceCol1 and SourceCol2.
Now when we write into the destination, EtlKit will see that there is one property on the ExpandoObject which name mathces
with the destination column: "DestColSum". The other two properties (SourceCol1 and SourceCol2) will be ignored, and data
from DestColSum will be written into the target.

*Note*: Of course you could have create a new ExpandoObject in the RowTransformation, which would have contained the
property DestColSum.

## Working with Arrays

Wworking with dynamic types can sometimes be a hazzle. EtlKit offers a third way to create your data flow without
defining object types and the need to create a POCO for your data. Simple use an array as data type - either an array
of type object or string. An string array could have advantages if you read data from json or csvs, object could work better
when reading from databases.

Here is an example for reading data from a file.

```csharp
CsvSource<string[]> source = new CsvSource<string[]>("text.csv");
RowTransformation<string[], row = new RowTransformation( 
    row => {
        row[0] = row[0] + ".test";
        row[2] = row[2] * 7;
        }
);
DbDestination<string[]> dest = new DbDestination<string[]>("DestinationTable");
```

In this example, you would have all data from the first column in your csv file accessible at the first position of the string array,
and so on. All your data will be automatically converted into a string data type.
This will also work for a DbDestination - the string data will then automatically be converted into back into the
right data type. Of course you will get an error if data types won't match (e.g. if you want to store the value "xyz" in
an integer column).

This approach is very useful when reading from a source where you get only string data, e.g. CSV or Json.
You can use a `RowTransformation` if you want to convert your string array into an object.

```csharp
RowTransformation<string[], MySimpleRow> = new RowTransformation<string[], MySimpleRow>( 
    row => {
        new MySimpleRow() {
            Col1 = row[0];
            Value2 = int.Parse(row[1]);
        }
);
```
