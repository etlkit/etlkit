using EtlKit.Primitives;

using EtlKit.Common.ControlFlow;
using EtlKit.ControlFlow;

namespace EtlKit.Logging
{
    /// <summary>
    /// This task will create a table that can store exceptions (and information about the affected records)
    /// that occur during a data flow execution
    /// </summary>
    [PublicAPI]
    public sealed class CreateErrorTableTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => "Create error table";

        public string TableName { get; set; }

        public bool DropAndCreateTable { get; set; }

        public void Execute()
        {
            if (DropAndCreateTable)
                DropTableTask.DropIfExists(ConnectionManager, TableName);

            CreateTableTask.Create(
                ConnectionManager,
                TableName,
                new List<TableColumn>
                {
                    new("ErrorText", "TEXT", allowNulls: false),
                    new("RecordAsJson", "TEXT", allowNulls: true),
                    new("ReportTime", "DATETIME", allowNulls: false)
                }
            );
        }

        public CreateErrorTableTask() { }

        public CreateErrorTableTask(string tableName)
        {
            TableName = tableName;
        }

        public CreateErrorTableTask(IConnectionManager connectionManager, string tableName)
            : this(tableName)
        {
            ConnectionManager = connectionManager;
        }

        public static void Create(IConnectionManager connectionManager, string tableName) =>
            new CreateErrorTableTask(connectionManager, tableName).Execute();

        public static void Create(string tableName) =>
            new CreateErrorTableTask(tableName).Execute();

        public static void DropAndCreate(IConnectionManager connectionManager, string tableName) =>
            new CreateErrorTableTask(connectionManager, tableName)
            {
                DropAndCreateTable = true
            }.Execute();

        public static void DropAndCreate(string tableName) =>
            new CreateErrorTableTask(tableName) { DropAndCreateTable = true }.Execute();
    }
}
