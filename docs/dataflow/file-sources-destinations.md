# Integration of flat files and web services

## Resource Type and Web Requests

All sources and destinations in this artice can be set to work either with a file
or to use data from a webservice. If you want to access a file on your drive or a network share,
use the componenten with the `ResourceType.File` option.
This is default for CsvSource/CsvDestination, but not for the XmlSource/XmlDestination or JsonSource/JsonDestination.
The other option is `ResourceType.Http` - and allows you to read data from a web service.
Instead of a filename just provide a Url. Furthermore, the components also have
a `[HttpClient](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=netframework-4.8)` property
that can be used to configure the Http request, e.g. to add authentication or use https instead.

## Csv

### CsvSource

A CcsvSource simple reads data from a CSV file.
It is based on the [library CsvHelper created by Josh Close](https://joshclose.github.io/CsvHelper/).
In the following examples, you will learn how to configure the CsvReader to your needs.
See the documentation of CsvHelper to learn more about the configuration options for the CsvReader itself.

Let's start with a simple example:

```csharp
CsvSource source = new CsvSource("Demo.csv");
source.Configuration.Delimiter = ";";
source.Configuration.IgnoreBlankLines = true;
```

This will creata a source component that reads the data from a "Demo.csv" file. This file could look like this:

```csharp
Row_Nr;Value
1;Test1
2;Test2
```

There are several configuration options for the Reader that you can set in the Configuration property. Learn more
about these options [in the CsvHelper.Configuration api documentation](https://joshclose.github.io/CsvHelper/api/CsvHelper.Configuration/Configuration/).
The default output data type of the CsvSource is an ExpandoObject. This is a dynamic object which will contain a property
for each column in your csv file. The first row of your file is supposed to be a header record (unless you use the SkipRows property to define how many
rows needs to be skipped before your header starts). The header will define the property names of the ExpandoObject.

You can now use a `RowTransformation` to transform it into the data type you need, or just stick with the ExpandoObject. (All other components
in EtlKit will also support this).

This is an example to transform the dynamic object into a regular .NET object:

```csharp
 CsvSource<ExpandoObject> source = new CsvSource<ExpandoObject>("Demo.csv");
RowTransformation<ExpandoObject,MyDataObject> trans = new RowTransformation<ExpandoObject,MyDataObject>(
    csvdata =>
    {
        dynamic csvRow = csvdata as ExpandoObject;
        MyDataObject myData = new MyDataObject() {
            Id = csvRow.Row_Nr,
            Value = csvRow.Value
        };
        return myData;
    });
```

#### Using object types

Of course you can use your data object as type for the CsvSource. The following code would directly read the data from the csv file
into the right object type.

```csharp
public class MyCsvData {
    public int Row_Nr { get; set; }
    public string Value { get; set; }
}
CsvSource<MyCsvData> source = new CsvSource<MyCsvData>("Demo.csv");
```

EtlKit will find the right property by the equivalent header column in your file. Therefore, the order of the columns doesn't matter, as long
as the column has an equivalent header. If the header name is different, you can use attributes or a ClassMap to find the right column.
Here is an example for using the Name and index attribute:

```csharp
public class MyCsvData {
    [Name("Row_nr")]
    public int Id { get; set; }
    [Index(1)]
    public string Text { get;set;}
}
CsvSource<MyCsvData> source = new CsvSource<MyCsvData>("Demo.csv");
```

See the full documentation [about CsvHelepr attributes here](https://joshclose.github.io/CsvHelper/examples/configuration/attributes) or
read more [about class maps](https://joshclose.github.io/CsvHelper/examples/configuration).

#### Using arrays

Sometimes it can be easier to use a string array (or object array) to read from a csv file, e.g. if your Csv file doesn't have a header.
EtlKit will support arrays as well - just define your CsvSource like this

```csharp
CsvSource<string[]> source = new CsvSource<string[]>("Demo.csv");
source.Configuration.HasHeaderRecord = false;
```

### CsvDestination

A CSV destination will create a file containing the data in the desired CSV format.
Like the CsvSource it is based on the [library CsvHelper created by Josh Close](https://joshclose.github.io/CsvHelper/).

The CsvDestination will work with the dynamic (ExpandoObject) as well as with regular object or arrays.
Here is an example how you can use a classic object to write data into a Csv file:

```csharp
 public class MySimpleRow
{    
    [Name("Header1")]
    [Index(1)]
    public int Col1 { get; set; }
    [Name("Header2")]
    [Index(2)]
    public string Col2 { get; set; }
}

CsvDestination<MySimpleRow> dest = new CsvDestination<MySimpleRow>("./SimpleWithObject.csv");
```

will create a .csv file like this

```csharp
Header1,Header2
1,Test1
2,Test2
3,Test3
```

If you use the ExpandoObject, the header names will be derived from the property names. In most cases, this will work as expected.
If you use an array, e.g. `CsvDestination<string[]>`, you won't get a header column.

## Json

### JsonSource

Json Source let you read a json. It is based on the `Newtonsoft.Json` and the `JsonSerializer`

Here an example for a json file:

```csharp
[
  {
    "Col1": 1,
    "Col2": "Test1"
  },
  {
    "Col1": 2,
    "Col2": "Test2"
  },
  {
    "Col1": 3,
    "Col2": "Test3"
  }
]
```

Here is some code that would read that json (and deserialize it into the object type):

```csharp
public class MySimpleRow
{
    public int Col1 { get; set; }
    public string Col2 { get; set; }
}

JsonSource<MySimpleRow> source = new JsonSource<MySimpleRow>("http://test.com/");
```

This code would then read the three entries from the source and post it into it's connected component.

### Nested arrays

The array doesn't need to be the top node of your json - it could be nested in your json file.
Like this:

```csharp
{
    "data": {
        "array": [
            {
                "Col1": 1,
                "Col2": "Test1"
            },
            ...
        ]
    }
}
```

EtlKit automatically scans the incoming json file and starts reading (and deserializing) after the
first occurence of the begin of an array (which is the "[" symbol).

### Working with JsonSerializer

Sometimes you have a more complex json structure. Here an example:

```csharp
[
    {
        "Column1": 1,
        "Column2": {
            "Id": "A",
            "Value": "Test1"
        }
    },
    ...
]
```

If you defined your POCOs types to deserialize this json you would need to create objects like this:

```csharp
public class MyRow
{
    public int Column1 { get; set; }
    public MyIdValueObject Column2 { get; set; }
}

public class MyIdValueObject
{
    public string Id { get; set; }
    public string Value { get; set; }
}
```

Sometimes you don't want to specify all objects that would map your json structure. To get around
this the underlying JsonSerializer object that is used for deserialization
is exposed by the JsonSource. [`JsonSerializer` belongs to Newtonsoft.Json](https://www.newtonsoft.com/json/help/html/SerializingJSON.htm)  
You could add your own JsonConverter so that you could use JsonPath within your JsonProperty attributes.
(Please note that the example JsonPathConverter is also part of EtlKit).

```csharp
[JsonConverter(typeof(JsonPathConverter))]
public class MySimpleRow
{
    [JsonProperty("Column1")]
    public int Col1 { get; set; }
    [JsonProperty("Column2.Value")]
    public string Col2 { get; set; }
}

JsonSource<MySimpleRow> source = new JsonSource<MySimpleRow>("res/JsonSource/NestedData.json", ResourceType.File);
```

### JsonDestination

The result of your pipeline can be written as json using a JsonDestination.

The following code:

```csharp
 public class MySimpleRow
{
    public string Col2 { get; set; }
    public int Col1 { get; set; }
}

JsonDestination<MySimpleRow> dest = new JsonDestination<MySimpleRow>("test.json", ResourceType.File);
```

would result in the following json:

```csharp
[
  {
    "Col1": 1,
    "Col2": "Test1"
  },
  {
    "Col1": 2,
    "Col2": "Test2"
  },
  {
    "Col1": 3,
    "Col2": "Test3"
  }
]
```

Like the JsonSource you can modify the exposed `JsonSerializer` to modfiy the serializing behaviour.
[`JsonSerializer` belongs to Newtonsoft.Json](https://www.newtonsoft.com/json/help/html/SerializingJSON.htm)

### Using Json with arrays

If you don't want to use objects, you can use arrays with your `JsonSource`. Your code would look like this:

```csharp
JsonSource<string[]> source = new JsonSource<string[]>("https://jsonplaceholder.typicode.com/todos");
```

Now you either have to override the `JsonSerializer` yourself in order to properly convert the json into a string[].
Or your incoming Json has to be in following format:

```csharp
[
    [
        "1",
        "Test1"
    ],
    ...
]
```

### Working with dynamic objects

JsonSource and destination support the usage of dynamic object. This allows you to use
a dynamic ExpandoObject instead of a POCO.

```csharp
JsonSource<ExpandoObject> source = new JsonSource<ExpandoObject>("res/JsonSource/TwoColumnsDifferentNames.json", ResourceType.File);

RowTransformation<ExpandoObject> trans = new RowTransformation<ExpandoObject>(
    row =>
    {
        dynamic r = row as ExpandoObject;
        r.Col1 = r.Value1;
        r.Col2 = r.Value2;
        return r;
    });
DbDestination<ExpandoObject> dest = new DbDestination<ExpandoObject>(Connection, "DynamicJson");

source.LinkTo(trans).LinkTo(dest);
source.Execute();
dest.Wait();
```

## Xml

### XmlSource

The xml source let you read data from a xml source. It will use `System.Xml` under the hood.

Let's assume your xml file looks like this:

```csharp
<?xml version="1.0" encoding="utf-8"?>
<Root>
    <MySimpleRow Col1="1">
        <Col2>Test1</Col2>
    </MySimpleRow>
    <MySimpleRow Col1="2">
        <Col2>Test2</Col2>
    </MySimpleRow>
</Root>
```

Xml reading is based on the Micrsoft XmlSerializer (using System.Xml.Serialization). You can make use of the default xml attribute
annotations to influence how data is read by the XmlSerializer. For the example xml above, the following code could read the xml file:

```csharp
[XmlRoot("MySimpleRow")]
public class MyRow
{
    [XmlAttribute]
    public int Col1 { get; set; }
    public string Col2 { get; set; }
}

XmlSource<MyRow> source = new XmlSource<MyRow>("source.xml", ResourceType.File);
```

#### Using dynamic objects

XmlSource does also support the dynamic ExpandoObject. If you want to use it, you can define an ElementName that contains the data you actually
want to parse - as you normally are not interested in your root element. EtlKit then will look for this Element and parse every occurence of
it into an ExpandoObject and send it into the connected components.

Here is an example. If your xml looks like this:

```csharp
<?xml version="1.0" encoding="utf-8"?>
<Root>
    <MySimpleRow>
        <Column1>1</Column1>
        <Column2>Test1</Column2>
    </MySimpleRow>
    <MySimpleRow>
        <Column1>2</Column1>
        <Column2>Test2</Column2>
    </MySimpleRow>
<Root>
```

You can parse the two elements  with the follwoing code:

```csharp
XmlSource source = new XmlSource("demo2.xml", ResourceType.File)
{
    ElementName = "MySimpleRow"
};
```

### XmlDestination

The xml destination will use the same XmlSerializer to serialize the data and write them into an xml file.

Here is an example how to influence the XmlSerializer using attributes:

```csharp
[XmlRoot("MySimpleRow")]
public class MyRow
{
    [XmlAttribute]
    public int Col1 { get; set; }
    [XmlAttribute]
    public string Col2 { get; set; }
}

XmlDestination<MyRow> dest = new XmlDestination<MyRow>("dest.xml", ResourceType.File);
```

Could create an output that looks like this:

```csharp
<?xml version="1.0" encoding="utf-8"?>
<Root>
  <MySimpleRow Col1="1" Col2="Test1" />
  <MySimpleRow Col1="2" />
</Root>
```

## Excel

### ExcelSource

An Excel source reads data from a xls or xlsx file.
[It is based the 3rd party library `ExcelDataReader`](https://github.com/ExcelDataReader/ExcelDataReader).
By default the excel reader will try to read all data in the file. You can specify a sheet name and a range
to restrict this behavior.

By default, a header column is expected in the first row. The name of the header for each columns
is used to map the column with the object - if the property is equal the header name, the value of
subsequent rows is written into the property.

Let's consider an example. If your excel file looks like this:

| Col1 | Col2 |
| --- | --- |
| 1 | Test1 |
| 2 | Test2 |
| 3 | Test3 |

You can easily load this data with an object like this:

```csharp
public class ExcelData {
    public string Col1 { get; set; }
    public int Col2 { get; set; }
}

ExcelSource<ExcelData> source = new ExcelSource<ExcelData> ("src/DataFlow/ExcelDataFile.xlsx");
```

You can change this behaviour with the Attribute `ExcelColumn`.
Here you can either define a different header name used for matching for a property.
Or you can set the column index for the property - the first column would be 0, the 2nd column 1, ...
When you using the column index, you can read also from ExcelFile that have no header row.
In this case, you need to set the property `HasNoHeader` to true when using the ExcelSource.

Usage example for an excel file that contains no header. This could like this:

|  |  |
| --- | --- |
| 1 | Test1 |
| 2 | Test2 |
| 3 | Test3 |

This is the corresponding object creation:

```csharp
public class ExcelData {
    [ExcelColumn(0)]
    public string Col1 { get; set; }
    [ExcelColumn(1)]
    public int Col2 { get; set; }
}

ExcelSource<ExcelData> source = new ExcelSource<ExcelData>("src/DataFlow/ExcelDataFile.xlsx") {
    Range = new ExcelRange(2, 4, 5, 9),
    SheetName = "Sheet2",
    HasNoHeader = true
};
```

The ExcelRange must not define the full range. It is sufficient if you just set the starting coordinates. The end of the
data can be automatically determined from the underlying ExcelDataReader.

The ExcelSource has a property `IgnoreBlankRows`. This can be set to true, and all rows which cells are completely empty
are ignored when reading data from your source.

#### Using dynamic objects

The ExcelSource comes like all other components with with the abitilty to work with dynamic object.

Just define your ExcelSource like this:

```csharp
ExcelSource source = new ExcelSource("src/DataFlow/ExcelDataFile.xlsx");
```

This will internally create an ExpandoObject for further processing. The property name will automatically be determined by the header column. If you don't have a header column, the property names would be `Column1` for the first, `Column2` for the second column and so on.
