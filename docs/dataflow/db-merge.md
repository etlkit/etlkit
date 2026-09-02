# Syncing tables with Merge

EtlKit can be used to integrate data from different source and write them into different destinations.
Most of the time you will use tables in a database as target.
A very common use case here is to keep a source and destination tables "in sync".
The following article describes how you can use the data from your data flow to insert
new data in a destination table, update existing or delete removed records.

Please note that for the sake of simplification we use a Database table as source which we want to
sync with a destination Database table. But of course the source doesn’t necessarily needs to be a table -
every data that comes from any source or transformation in your data flow can be used as source for syncing.

## Full or Delta

First, let's differentiate 2 scenarios.

Scenario 1: The source table does not have any information about its changes.
So no delta information is provided by the source. The source is delivering data always in "full".

Scenario 2: The source table has information about its changes.
This can be e.g. a timestamp indicating when the record was inserted or updated. The source contains information
about it's changes, which is called "delta".

Scenario 2 is a little bit more tricky when it comes to deletions. In scenario 1 we always know which objects are currently
existing and what data they have. In the Delta scenario the handling of deletions are more problematic. There is
no straight-forward solution how to manage deleted records here. One way could be an extra flag indicating that
the record was deleted (including the deletion time as "update" timestamp).
Or it could be that deletions are not transferred at all, either because they don't happen or for other reasons.

### DbMerge

Both scenarios are supported with EtlKit. The `DbMerge` component can be used to tackle this problem

The `DbMerge` is a destination component and is created on a destination table in your dataflow.
It will wait for all data from the flow to arrive, and then either insert,
update or delete the data in the destination table.
Deletion is optional (by default turned on), and can be disabled by setting the property
`DeltaMode` to `DeltaMode.NoDeletions`.

The DbMerge was designed for scenario 1 and scenario 2. For scenario 2, the property DeltaMode has
to be set to DeltaMode.Delta: `DeltaMode = DeltaMode.Delta`.

## Example

### Data and object definition

To implement an example sync between two tables, we will need a `DbSource` pointing to our source table.
In our case we just pass a table name for the source table, but you could also define a sql query
(e.g. which gives you only the delta records).

The source is then connected to the DbMerge, and data from the source will then be inserted,
updated or deleted in the destination.

The DbMerge is a generic object and expect as type an object that implements the interface `IMergeableRow`.
This interface needs to have a ChangeDate and ChangeAction defines on your object, as well a UniqueId property
to describe how objects are compared.

The easiest (and recommended) way to implement is the interface is to inherit from the class `MergeableRow`.
You will automatically have all the necessary implementation details to pass the object to a `DbMerge`.
Only two things are left to do here:

1. You need to flag the properties that identify the unique Id columns with the attribute `IdColumn`
2. You need to flag the properties used when comparing the values of a record to decide if it really needs to be updated
   with the attribute `CompareColumn`

Let's start with a simple object, that has a property that should contain the key column (the id) and one property
for holding a value:

```csharp
public class MyMergeRow : MergeableRow
{
    [IdColumn]
    public int Key { get; set; }

    [CompareColumn]
    public string Value { get; set; }
}
```

In our scenario we have a source table that would look like this:

| Key | Value |
| --- | --- |
| 1 | Test - Insert |
| 2 | Test - Update |
| 3 | Test - Exists |

And the destination table would like this:

| Key | Value |
| --- | --- |
| 2 | XXX |
| 3 | Test - Exists |
| 4 | Test - Deleted |

### Setting up the data flow

No we can already set up a data flow. It would look like this:

```csharp
DbSource<MyMergeRow> source = new DbSource<MyMergeRow>(connection, "SourceTable");
DbMerge<MyMergeRow> merge = new DbMerge<MyMergeRow>(connection, "DestinationTable");
source.LinkTo(merge);
source.Execute();
merge.Wait();
```

Now what happens if we let this flow run? First of all, all records will be loaded from the destination
into a memory object and compared with the source data. Within the memory object, the DbMerge
will identify:

* which records need to inserted (ChangeAction: Insert)
* which records need to be updated (ChangeAction: Update)
* which records exists and doesn't need to be updated (ChangeAction: Exists)
* which record needs to be deleted (ChangeAction: Delete), if deletions are allowed

To identify these different options, the `IdColumn` is used. In our example the id column is a unique primary key,
and it is recommended to only use unique columns for that.

As you can see, there is a difference between an update and an existing records that doesn't need to be updated.
All records where the IdColumns match will be examined based on their value. All properties
marked with the `CompareColumn` attribute are compared (using the underlying Equals() implementation).
If one property/columns differs, the record will be marked to be updated. If they are all equal, the record
won't be touched on the database and marked as 'E' (Existing).

After this comparison is done, it will start to write the changes into the databases (in batches)
First, it will delete all records marked as 'D' (Deleted) or 'U' (Updated) from the database.
Then it will write the updated records 'I' and 'U' back into the destination table. (As you can see, updates are done
by deleting and inserting the record again). Records that doesn't need to be updated are left in the destination table.

In our example after doing the `DbMerge`, our destination table now looks like this:

| Key | Value |
| --- | --- |
| 1 | Test - Insert |
| 2 | Test - Update |
| 3 | Test - Exists |

Please note that if you connect the DbMerge to a source that provide you with delta information only,
you need to disable the deletions - in that case, deletions need to be handled manually. If we would have
deletions disable, there would be an additional row in our destination table:

| Key | Value |
| --- | --- |
| 4 | Test - Deleted |

### Delta table

The DbMerge has a property `DeltaTable` which is a List containing additionally information what records
where updated, existing, inserted or deleted. The operation and change-date is stored in the corresponding
`ChangeDate`/ `ChangeAction` properties.

In our example, the property `dest.DeltaTable` now contains information about the what delta operations where made on the destination
table. In this example, it would contain the information, that 1 row was inserted (Key: 1)
, 1 was updated (Key: 2), one column wasn't changed (Key:3) and one column was deleted (Key: 4).

This information can be used as a source for further processing in the data flow,
simple by connecting the DbMerge to a transformation or another Destination. So our complete flow could look like this:

```csharp
DbSource<MyMergeRow> source = new DbSource<MyMergeRow>(connection, "SourceTable");
DbMerge<MyMergeRow> merge = new DbMerge<MyMergeRow>(connection, "DestinationTable");
DbDestination<MyMergeRow> delta = new DbDestination<MyMergeRow>(connection, "DeltaTable");
source.LinkTo(merge);
merge.LinkTo(delta);
source.Execute();
merge.Wait();
delta.Wait();
```

The DeltaTable now will look like this:

| Key | ChangeDate | ChangeAction |
| --- | --- | --- |
| 1 | 2019-01-01 12:00:01 | Insert (1) |
| 2 | 2019-01-01 12:00:02 | Update (2) |
| 3 | 2019-01-01 12:00:02 | Exists (0) |
| 4 | 2019-01-01 12:00:03 | Delete (3) |

## Additional configurations

### Truncate instead delete

Because the DbMerge does delete records that need to be deleted or updated using a `DELETE` sql statement,
this method can sometimes be a performance bottleneck if you expect a lot of deletions to happen. The
`DbMerge` does support a Truncate-approach by setting the property `UseTruncateMethod` to true.
It will then read all existing data from the destination into the memory, identify the changes,
truncate the destination table and then write all changes back into the database. This approach can be much faster
if you expect a lot of deletions, but you will always read all data from the destination table and write it back.
Unfortunately, there is no general recommendation when to use this approach.

Also, if you don't specify any Id columns with teh `IdColumn` attribute, the DbMerge will use the truncate method automatically.

### Delta mode

If the source transfer delta information, then you can set the DbMerge delta mode:

```csharp
DbMerge<MyMergeRow> dest = new DbMerge<MyMergeRow>(connection, "DbMergeDeltaDestination")
{
    DeltaMode = DeltaMode.Delta
};
```

In delta mode, by default objects in the destination won't be deleted. It can be that there is a property in your source
that is an indicator that a record is deleted. In this case, you can flag this property with the attribute `DeleteColumn`.

```csharp
public class MyMergeRow : MergeableRow
{
    [IdColumn]
    public long Key { get; set; }
    [CompareColumn]
    public string Value { get; set; }
    [DeleteColumn(true)]
    public bool DeleteThisRow { get; set; }
}
```

In this example object, if the property DeleteThisRow is set to true, the record in the destination will be deleted
if it matches with the Key property that is flagged with the attribute IdColumn.

### ColumnMap attribute

If the columns have different names than our property, we need to add the `ColumnMap` attribute to have them
mapped correctly. If the columns would be named Col1 for the Key and Col2 for the Value, our object would look like this:

```csharp
public class MyMergeRow : MergeableRow
{
    [IdColumn]
    [ColumnMap("Col1")]
    public int Key { get; set; }

    [CompareColumn]
    [ColumnMap("Col2")]
    public string Value { get; set; }
}
```

### Composite Keys

Composite keys are supported: just flag all the columns that you want to use as composite unique key with the
`IdColumn` attribute. Internally, all properties are concatenated to a string by using the ToString() method of the properties.
This concatenated string is then used as identifier for each record.

```csharp
public class MyMergeRow : MergeableRow
{
    [IdColumn]
    public long ColKey1 { get; set; }

    [IdColumn]
    public string ColKey2 { get; set; }

    [CompareColumn]
    public string ColValue1 { get; set; }

    [CompareColumn]
    public string ColValue2 { get; set; }
}
```

As you can see, you can also use the `CompareColumn` attribute on each property that you want to use for identifying
existing records.

### Using the IMergeableRow interface

Sometimes, you want do the implementation of the IMergeableRow interface yourself. Here is an example implementation:

```csharp
public class MySimpleRow : IMergeableRow
{
    [IdColumn]
    public long Key { get; set; }
    public string Value { get; set; }
    public DateTime ChangeDate { get; set; }
    public ChangeAction? ChangeAction { get; set; }
    public string UniqueId => Key.ToString();
    public new bool Equals(object other)
    {
        var o = other as MySimpleRow;
        if (o == null) return false;
        return Value == o.Value;
    }
}
```

Overwriting the Equals method and using the IdColumn attribute is optional. If no IdColumn
is passed, the truncate method is used to merge the data.
