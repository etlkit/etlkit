# Logging

EtlKit logs through
[Microsoft.Extensions.Logging](https://learn.microsoft.com/dotnet/core/extensions/logging). Every
control flow task and data flow component writes to an `ILogger` obtained from a shared
`ILoggerFactory`. Because the abstraction is the standard .NET one, you can plug in any logging
provider you already use — console, file, [Serilog](https://serilog.net/), Application Insights, and
so on — exactly as you would in any other .NET application.

By default the factory is a `NullLoggerFactory`, so **nothing is logged until you configure a logger
factory**.

## Enabling logging

Set the static `ControlFlow.LoggerFactory` property to a configured `ILoggerFactory`. The example
below sends all log output to the console:

```csharp
using EtlKit.Common.ControlFlow;
using Microsoft.Extensions.Logging;

ControlFlow.LoggerFactory = LoggerFactory.Create(builder =>
    builder
        .SetMinimumLevel(LogLevel.Information)
        .AddConsole());
```

Once the factory is set, every task and component produces log output through it. Swap
`AddConsole()` for any other provider to route the output elsewhere.

> `AddConsole()` comes from the `Microsoft.Extensions.Logging.Console` NuGet package. Other providers
> live in their own packages (for example `Serilog.Extensions.Logging` for Serilog).

### Injecting a logger per component

In addition to the global factory, you can inject an `ILogger<T>` directly into a component or task
through its constructor. A component that receives a logger uses it; otherwise it falls back to
`ControlFlow.LoggerFactory`. This is convenient when your application already resolves loggers from a
dependency injection container.

### Structured log properties

Every log entry EtlKit writes carries a set of structured properties alongside the message. Any
provider that supports structured logging can capture them:

| Property         | Meaning                                                                  |
| ---------------- | ------------------------------------------------------------------------ |
| `Type`           | The class name of the task or component that produced the entry          |
| `Action`         | `START` (first message), `END` (component finished), `RUN` or `LOG`      |
| `Hash`           | A hash of the task or component, derived from its type and name          |
| `Stage`          | The current value of `ControlFlow.Stage` (see below)                     |
| `LoadProcessKey` | The id of the current load process, if one was started (see below)       |

### Setting the logging stage

One thing you perhaps want to know in your logging output is the current stage of your data
processing. Think of this as a category for your log output — if things happen during the setup of
your database, the stage could be "SETUP"; if you are currently staging your data from your sources
into your database the stage could be "STAGING".

To set or modify the current stage, change the static property `Stage` on the `ControlFlow` class.
Its value is attached to every subsequent log entry as the `Stage` property:

```csharp
ControlFlow.Stage = "SETUP";
//some logging
ControlFlow.Stage = "STAGING";
```

### Disable logging

Perhaps you want some particular tasks or components not to produce any log output, but you don't
want to remove the logging completely. For this case you can use the `DisableLogging` property on
every task or component in EtlKit. E.g., if you create a new `DbSource`, just set the property to
true:

```csharp
DbSource source = new DbSource("TableName") { DisableLogging = true };
```

If you want to disable logging in general for all tasks, you can set the static property
`DisableAllLogging` on the `ControlFlow` class:

```csharp
ControlFlow.DisableAllLogging = true;
```

Whenever set to true, no logging output will be produced. When set back to false, logging will be
activated again.

### Controlling data flow log volume

Data flow components emit a progress entry every `DataFlow.LoggingThresholdRows` rows (default
`1000`), plus a `START` and `END` entry per component. Lower the threshold to log more often, or set
it to `null`/`0` to suppress the per-row progress entries:

```csharp
DataFlow.LoggingThresholdRows = 100; // log progress every 100 rows
```

## Logging to a database

Console or file output is perhaps not sufficient. If you want your log entries stored in a database
table, EtlKit ships an optional package, `EtlKit.Logging.Database`, that configures the logger
factory to write into a log table for you.

There are two steps: create the log table, then point the logger factory at it.

```csharp
using EtlKit.Logging;          // CreateLogTableTask
using EtlKit.Logging.Database; // DatabaseLoggingConfiguration

var connection = new SqlConnectionManager("Data Source=.;Integrated Security=SSPI;Initial Catalog=EtlKit_Logging");

CreateLogTableTask.Create(connection);
DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(connection);
```

`CreateLogTableTask` creates the logging table. Its structure looks like this:

| Column name     | Data type | Remarks                        |
| --------------- | --------- | ------------------------------ |
| id              | Int64     | Identity                       |
| log_date        | DateTime  |                                |
| level           | String    |                                |
| stage           | String    |                                |
| message         | String    |                                |
| task_type       | String    |                                |
| task_action     | String    |                                |
| task_hash       | String    |                                |
| source          | String    |                                |
| load_process_id | Int64     | Id of etlkit_loadprocess table |

This works on all supported databases — the real data type is mapped to the corresponding
database-specific type. For example, `Int64` is a `BIGINT` on Sql Server and an `INTEGER` on SQLite.
The `id` column is an identity (auto increment) column and the only one that is not nullable.

`AddDatabaseLoggingConfiguration` replaces `ControlFlow.LoggerFactory` with one that writes every log
entry to this table. Internally this is the single place where EtlKit uses
[NLog](https://nlog-project.org) — it builds an NLog database target as the sink. NLog is an
implementation detail of this package, not the general logging mechanism. After calling it, any task
or component that produces log output is automatically logged to the table.

> Because database output is ultimately just another logging provider, advanced users can skip this
> helper and configure their own provider (NLog, Serilog, etc.) with a database sink on
> `ControlFlow.LoggerFactory` directly.

#### Log table name

By default the logging table is named `etlkit_log`. To use a different name, pass it to both
`CreateLogTableTask.Create` and `AddDatabaseLoggingConfiguration`. The minimum log level (default
`LogLevel.Information`) can also be passed — note this is the standard
`Microsoft.Extensions.Logging.LogLevel`:

```csharp
CreateLogTableTask.Create(connection, "mylogtable");
DatabaseLoggingConfiguration.AddDatabaseLoggingConfiguration(connection, LogLevel.Debug, "mylogtable");
```

### Log output

After you configured database logging (or any other provider such as file or console), all log
messages generated by any control flow task or data flow component are sent to that target.

The following example creates four rows in your log table. Every time a task or component starts, it
creates a log entry with the action `START`; when it finishes, it creates another entry with the
action `END`:

```csharp
SqlTask.ExecuteNonQuery("some sql", "Select 1 as test");
Sequence.Execute("some custom code", () => { });
```

If you want to produce your own log output, you can use the `LogTask`. This creates a single entry
with the action `LOG`. The message here would be "Some warning!":

```csharp
LogTask.Warn("Some warning!");
```

You can pick the level with the log task. E.g.:

```csharp
LogTask.Trace("Some text!");
LogTask.Debug("Some text!");
LogTask.Info("Some text!");
LogTask.Warn("Some text!");
LogTask.Error("Some text!");
```

### Logging of load processes

Besides directing log messages to a target, EtlKit comes with a set of tasks to control your ETL
processes — so-called "load processes".

The use case for a load process table is simple — if you have one log table, this table stores log
messages for an ETL job. If the job runs again, more or less the same log information is written to
the log table, with different timestamps of course. If you need to identify which log entry relates
to which job run, some information is missing. This is where the load process table comes in.

You can use the task `CreateLoadProcessTableTask` to have EtlKit create a load process table.

```csharp
CreateLoadProcessTableTask.Create(connection);
```

By default this creates a table named `etlkit_loadprocess`. This table looks like this:

| Column name    | Data type | Remarks  |
| -------------- | --------- | -------- |
| id             | Int64     | Identity |
| start_date     | DateTime  |          |
| end_date       | DateTime  |          |
| source         | String    |          |
| process_name   | String    |          |
| start_message  | String    |          |
| is_running     | Int16     | 0 or 1   |
| end_message    | String    |          |
| was_successful | Int16     | 0 or 1   |
| abort_message  | String    |          |
| was_aborted    | Int16     | 0 or 1   |

The table contains information about the ETL processes that you started in your code with the
`StartLoadProcessTask`. To end or abort a process, there is the `EndLoadProcessTask` or
`AbortLoadProcessTask`.

Let's look at the following example for logging into the load process table.

```csharp
StartLoadProcessTask.Start("Process 1", "Starting process");

try {
/* ... some tasks or data flow */
   EndLoadProcessTask.End("The process ended successfully");
} catch (Exception e) {
   AbortLoadProcessTask.Abort(e.ToString());
}
```

After calling `StartLoadProcessTask`, a new entry is created in the `etlkit_loadprocess` table. This
entry has a start date and contains the process name "Process 1" and the start message "Starting
process". The column `is_running` is 1. Calling `EndLoadProcessTask` sets an end date and changes
`is_running` to 0 and `was_successful` to 1. Likewise, `AbortLoadProcessTask` sets `is_running` to 0
and `was_aborted` to 1. The abort message would contain the exception as a string.

When the load process entry is added to the table, a new id is created. All information about the
load process (including the id) can be accessed via the static property `CurrentLoadProcess` on the
`ControlFlow` class. Whenever a new log message for the log table is created, this entry also
contains the id of the current load process in the column `load_process_id`.

```csharp
LogTask.Warn("This will create a log message");
Console.WriteLine($"The log message will contain the current load process id: {ControlFlow.CurrentLoadProcess.Id}");
```

Whenever you end a process and start a new one, the `CurrentLoadProcess` property switches to the
current load process.

#### Load process table name

When you create the load process table and you don't pass a custom table name, by default the name
of the table is `etlkit_loadprocess`. You can change this by passing the table name to the
`CreateLoadProcessTableTask`. If the table is already created, you must specify the table name in the
static property `LoadProcessTable` of the `ControlFlow` class.

```csharp
//Will set the ControlFlow.CurrentLoadProcess property automatically:
CreateLoadProcessTableTask.Create(connection, "myloadprocesstable");

//If CreateLoadProcessTableTask was not executed:
ControlFlow.LoadProcessTable = "myloadprocesstable";
```

### Further log tasks

There are some more logging tasks that can be used to manage your log tables.

#### Get log and load process table in JSON

If you want to get the content of the load process table or the log table in JSON format, there are
two tasks for that:

```csharp
var jsonLoadProcesses = GetLoadProcessAsJSONTask.GetJSON();
var jsonLog = GetLogAsJSONTask.GetJSON();
```

#### Reading log and load process table programmatically

If you want to read the load process programmatically, you can use the `ReadLoadProcessTableTask`.
For accessing the log table, there is the `ReadLogTableTask`.

```csharp
//Get last aborted load process and read log entries
LoadProcess lastaborted = ReadLoadProcessTableTask.ReadWithOption(ReadOptions.ReadLastAborted);
List<LogEntry> allLogEntrieForLoadProcess = ReadLogTableTask.Read(connection, lastaborted.Id);

//Get all load processes and all log entries
List<LoadProcess> allLoadProcesses = ReadLoadProcessTableTask.ReadAll();
List<LogEntry> allLogEntries = ReadLogTableTask.Read(connection);
```
