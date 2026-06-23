# Logging

By default, EtlKit uses NLog. NLog already comes with different log targets that be configured
either via your app.config or programmatically. See the NLog-documentation for a full reference:
<https://nlog-project.org/>

EtlKit already comes with NLog as dependency - so the necessary packages will be retrieved from
nuget automatically through your package manager.

On top of NLog, EtlKit offers you support to create a simple but still powerful database logging,
which is simple to set up and eays to maintain.

## A simple Configuration File

In order to use logging, you have to create a `nlog.config` file (with this exact name) and put it
into the root folder of your project. Make sure that it is copied into your output directory.

A simple and very basic nlog.config would look like this

```csharp
<?xml version="1.0" encoding="utf-8"?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xsi:schemaLocation="NLog NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <rules>
    <logger name="*" minlevel="Info" writeTo="console" />
  </rules>
  <targets>
    <target name="console" xsi:type="Console" />
  </targets>
</nlog>
```

After adding a file with this configuration, you will already get some logging output to your
console output when you trigger some EtlKit components.

### Copy to output directory

Make sure the config file is copied into the output directory where you build executables are
dropped. Your project configuration file .csproj should contain something like this:

```csharp
<Itemgroup>
...
  <None Update="nlog.config">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</Itemgroup>
```

### Debugging logging issues

NLog normally behaves very "fault-tolerant". By default, if something is not setup properly or does
not work when NLog tries to log, it just "stops" working without throwing an exception or stopping
the execution. This behavior is very desirable in a production environment, but hard to debug.

If you need to debug Nlog, you can change the nlog root-element of the nlog.config into:

```csharp
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xsi:schemaLocation="NLog NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      throwConfigExceptions="true"
      autoReload="true"
      internalLogFile="console-example-internal.log"
      internalLogLevel="Info">
```

With this configuration it will raise an exception and also log it into a file.

## Logging into files

Nlog can be easily configured to have your logging output stored in files. Here is an example for a
simple configuration that will create a Log - folder within your application directory and have a
file for every error level provided by Nlog. (NLog does differentiate each log entry by a log level,
which are: Trace, Debug, Info, Warn, Error and Fatal.)

```csharp
<?xml version="1.0" encoding="utf-8"?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xsi:schemaLocation="NLog NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <targets>
    <target name="file" xsi:type="AsyncWrapper"
            queueLimit="5000"
            overflowAction="Discard">
      <target xsi:type="File" fileName="${basedir}/logs/${level}.txt"
              deleteOldFileOnStartup="true"
              layout="${longdate}|${pad:padding=10:fixedLength=true:inner=${etllog:LogType=STAGE}}|${pad:padding=20:fixedLength=true:inner=${etllog:LogType=Type}}|${pad:padding=5:fixedLength=true:inner=${etllog:LogType=Action}}|${etllog}" />
    </target>
  </targets>
  <rules>
    <logger name="*" minlevel="Debug" writeTo="file" />
  </rules>
</nlog>
```

As you can see, the log output into the file is formatted using a particular NLog notation in the
Layout attribute. NLog does provide some default LayoutRenderer here. Additionally, EtlKit will
register also some layout renderer which can be used. These are:

```csharp
//The default log message of the component or task:
layout="${etllog}"

//The current value defined in ControlFlow.Stage:
layout="${etllog:LogType=Stage}"

//The class name of the task or component that produces the log output:
layout="${etllog:LogType=Type}"

 //A component can can produce more than on log message.
 //Actions can be 'START' (first log message), 'END' (component finished), 'RUN', 'LOG':
 layout="${etllog:LogType=Action}

 //The hash value of the specific task or component, derived from the type and the name
 layout="${etllog:LogType=Hash}"

//If a load process was started, the load process id is in here
layout="${etllog:LogType=LoadProcessKey}" />
```

The details for each layout renderer will explained in more details in the following chapters.

### Setting the logging stage

One thing you perhaps want to know in your logging output is the current stage of your data
processing. Think you this as a category for your log output - if things happen during the setup of
your database, the Stage could be "SETUP" - if you are currently staging your data from your sources
into your database the stage could be "STAGING".

To set up or modify the current stage, just change the static property `Stage` in the `ControlFlow`
class:

```csharp
ControlFlow.STAGE = "SETUP"
//some logging
ControlFlow.STAGE = "STAGING"
```

### Disable logging

Perhaps you want some particular tasks or components not to produce any log output, but you don't
want to remove the logging completely. For this case you can use the `DisableLogging` property on
every task or component in EtlKit. E.g., if you create a new DbSource, just set the property to
true:

```csharp
DbSource source = new DbSource("TableName") { DisableLogging = true};
```

If you want to disable logging in general for all tasks, you can set the static property
`DisableAllLogging` in the ControlFlow class:

```csharp
ControlFlow.DisableAllLogging = true;
```

Whenever set to true, no logging output will be produced. When set back to false, logging will be
activated again.

## Logging to database

Of course logging to console output or to a file is perhaps not sufficient. If you want to have
logging tables in your database, EtlKit comes with some additions to the default logging mechanism
provided by NLog.

### Method 1: Extend the nlog.config

One way to have logging into the database enabled is to extend the nlog configuration and add your
database as target. This way is the most flexible one, but it involves some manual steps: You have
to set up the logging table yourself, and define the database target in your nlog.config file.

The modification to the nlog.config could like this:

```csharp
<?xml version="1.0" encoding="utf-8"?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xsi:schemaLocation="NLog NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <targets>
    <target xsi:type="Database" name="database"
       useTransactions="false" keepConnection="true">
      <commandText>
        INSERT INTO etllog (LogDate, Level, Stage, Message, TaskType, TaskAction, TaskHash, Source)
        SELECT @LogDate, @Level, @Stage, @Message, @Type, @Action, @Hash, @Logger
      </commandText>
      <parameter name="@LogDate" layout="${date:format=yyyy-MM-ddTHH\:mm\:ss.fff}" />
      <parameter name="@Level" layout="${level}" />
      <parameter name="@Stage" layout="${etllog:LogType=Stage}" />
      <parameter name="@Message" layout="${etllog}" />
      <parameter name="@Type" layout="${etllog:LogType=Type}" />
      <parameter name="@Action" layout="${etllog:LogType=Action}" />
      <parameter name="@Hash" layout="${etllog:LogType=Hash}" />
      <parameter name="@Logger" layout="${logger}" />
    </target>
  </targets>
  <rules>
    <logger name="*" minlevel="Info" writeTo="database" />
  </rules>
</nlog>
```

### Method 2: Let EtlKit do the work

Alternatively, you can use some pre-defined Logging tasks to update your nlog configuration and
create the logging table.

The following code snipped will do this for you:

```csharp
var SqlConnectionManager connection = new SqlConnectionManager("Data Source=.;Integrated Security=SSPI;Initial Catalog=EtlKit_Logging");
CreateLogTableTask.Create(connection);
ControlFlow.AddLoggingDatabaseToConfig(connection);
```

In this example, `CreateLogTableTask` will create a logging table. The table structure will look
like this:

| Column name     | Data type | Remarks                       |
| --------------- | --------- | ----------------------------- |
| id              | Int64     | Identity                      |
| log_date        | DateTime  |                               |
| level           | String    |                               |
| stage           | String    |                               |
| message         | String    |                               |
| task_type       | String    |                               |
| task_action     | String    |                               |
| task_hash       | String    |                               |
| source          | String    |                               |
| load_process_id | Int64     | Id of etlkit_loadprocess table |

This will work on all supported database - the real data type will be reflected by the corresponding
database specific type. E.g. Int64 is a BIGINT on SqlServer and INTEGER on SqlLite The id column is
an Identity (or auto increment) column, and the only one not nullable.

The `AddLoggingDatabaseToConfig` method will add the corresponding nlog database target to the nlog
configuration. This happens to the configuration that Nlog keeps after reading the nlog.config
file - the file itself will be untouched.

Now, if you call any task or component that creates log output, it will automatically be logged to
the newly create database table

#### Log table name

By default, the name for the logging table is "etlkit_log". If you want to change that name, the
`CreateLogTableTask` and `AddLoggingDatabaseToConfig` method do have a parameter logTableName that
can be set to your specifig table name. If you use a different name, before you call
`AddLoggingDatabaseToConfig` you have to set the table name in the static property LogTable of
`ControlFlow` class.

```csharp
ControlFlow.LogTable = "mylogtable";
ControlFlow.AddLoggingDatabaseToConfig(connection, NLog.LogLevel.Debug, "mylogtable");
```

Also, when using the `AddLoggingDatabaseToConfig`, you can define a min log level at which Nlog
start to produce log output. By default it is set to LogLevel "Info".

### Log output

After you added a log table (or all other nlog target like file or console output), all log message
generated from any control flow task or data flow component will be redirected to this target.

The following example will create 4 rows in your log table. Every time a tasks or component starts,
it will create a log entry with an action 'START'. When it's done with its execution, it will create
another log entry with action type 'END'

```csharp
SqlTask.ExecuteNonQuery("some sql", "Select 1 as test");
Sequence.Execute("some custom code", () => { });
```

If you want to produce your own log output, you can use the `LogTask`. This will create only one row
in your log output, with the TaskAction "LOG". The message here would be "Some warning!".

```csharp
LogTask.Warn("Some warning!");
```

Also you can define the nlog level with the log task. E.g.:\_

```csharp
LogTask.Trace("Some text!");
LogTask.Debug("Some text!");
LogTask.Info("Some text!");
LogTask.Warn("Some text!");
LogTask.Error("Some text!");
LogTask.Fatal("Some text!");
```

### Logging of Load Processes

Additionally to the traditional nlog setup where log information is send to any target by changing
the configuration, EtlKit comes with a set of Tasks to control your ETL processes - so called "Load
processes".

The use case for a load process table is simple - if you have one log table, this table will store a
log messages for an ETL job. If the job run agains, more or less the same log information is written
in the log table - with different timestamps of course. If you need to identify which log entry
relates to which job run, there are some information missing. This is where the load process table
comes in.

You can use the task `CreateLoadProcessTableTask` to have EtlKit created a load process table.

```csharp
CreateLoadProcessTableTask.Create(connection);
```

By default this will create a talbe "etlkit_loadprocess". This table will look like this:

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

The table will contain information about the ETL processes that you started in your code with the
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

After calling the `StartLoadProcessTask` a new entry was created in the etlkit_loadprocess table.
This entry had a start date and contained the process name "Process 1" and the start message
"Starting process". The column `is_running` is 0. Calling the `EndLoadProcessTask` will set an end
date and change the columns `is_running` to 0 and was_successful to 1. Vice versa will
`AbortLoadProcessTask` set `is_running` to 0 and `was_aborted` to 1. The abort message would contain
the exception as string.

When the load process entry is added to the table, a new id is created. All information about the
load process (including the id) can be accessed via the static property CurrentLoadProcess in the
ControlFlow class. Whenever a new log message for the log table is created, this entry will also
contain the id of the current load process in the column load_process_id.

```csharp
LogTask.Warn("This will create a log message")
Console.WriteLine("The log message will contain the current load process id: ControlFlow.CurrentLoadProcess.Id");
```

Whenever you end a process and start a new one, the CurrentLoadProcess property will switch to the
current load process.

#### Load process table name

When you create the load process table, and you don't pass a custom table name, by default the name
of the table is "etlkit_loadprocess". You can change this by passing the table name to the
`CreateLoadProcessTableTask`. If the table is already created, you must specify the Table name in
the static property LoadProcessTable of the ControlFlow class

```csharp
//Wil set the ControlFlow.CurrentLoadProcess property automatically:
CreateLoadProcessTableTask.Create(SqlConnection, "myloadprocesstable");

//If CreateLoadProcessTableTask was not executed:
ControlFlow.CurrentLoadProcess = "myloadprocesstable";
```

### Further log tasks

There are some more logging tasks that can be used to manage your log tables.

#### Get log and loadprocess table in JSON

If you want to get the content of the etl.LoadProcess table or etl.Log in JSON-Format, there are two
tasks for that:

```csharp
var jsonLoadProcesses = GetLoadProcessAsJSONTask.GetJSON();
var jsonLog = GetLogAsJSONTask.GetJSON();
```

#### Reading log and load process table programmatically

If you want to read the load process programmatically, you can use the `ReadLoadProcessTableTask`.
For accessing the log table, there is the `ReadLogTableTask`

```csharp
//Get last aborted load process and read log entries
LoadProcess lastaborted = ReadLoadProcessTableTask.ReadWithOption(ReadOptions.ReadLastAborted);
List<LogEntry> allLogEntrieForLoadProcess = ReadLogTableTask.Read(connection, lastaborted.Id);

//Get all load processes and all log entries
List<LoadProcess> allLoadProcesses = ReadLoadProcessTableTask.ReadAll();
List<LogEntry> allLogEntries = ReadLogTableTask.Read(connection);
```
