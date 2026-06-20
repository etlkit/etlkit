using System.IO;
using System.Linq;

using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow.SqlServer
{
    /// <summary>
    /// Restores a database from a backup.
    /// </summary>
    [PublicAPI]
    public class RestoreDatabaseTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName =>
            $"Restore DB {DatabaseName} from {Path.GetFullPath(FileName)}";

        public void Execute()
        {
            if (!DbConnectionManager.SupportDatabases)
                throw new ETLBoxNotSupportedException("This task is not supported!");

            DefaultDataPath = (string)
                new SqlTask(this, DefaultDataPathSql)
                {
                    TaskName = "Read default data path"
                }.ExecuteScalar();
            FileList = new List<BackupFile>();
            new SqlTask(this, FileListSql)
            {
                TaskName = $"Read file list in backup file {Path.GetFullPath(FileName)}",
                BeforeRowReadAction = () => CurrentBackupFile = new BackupFile(),
                AfterRowReadAction = () => FileList.Add(CurrentBackupFile),
                Actions = new List<Action<object>>
                {
                    logicalName => CurrentBackupFile.LogicalName = (string)logicalName,
                    type => CurrentBackupFile.Type = (string)type,
                    fileId => CurrentBackupFile.FileId = (long)fileId
                }
            }.ExecuteReader();
            new SqlTask(this, Sql).ExecuteNonQuery();
        }

        /* Public properties */
        public string DatabaseName { get; set; }
        public string FileName { get; set; }
        public string Sql
        {
            get
            {
                return $@"
USE [master]
RESTORE DATABASE [{DatabaseName}] FROM  DISK = N'{Path.GetFullPath(FileName)}' WITH FILE=1,
"
                    + string.Join(
                        "," + Environment.NewLine,
                        FileList
                            .OrderBy(file => file.FileId)
                            .Select(file =>
                                $"MOVE N'{file.LogicalName}' TO N'{Path.Combine(DefaultDataPath, DatabaseName + file.Suffix)}'"
                            )
                    )
                    + @"
, NOUNLOAD, REPLACE";
            }
        }

        /* Some constructors */
        public RestoreDatabaseTask() { }

        public RestoreDatabaseTask(string databaseName, string fileName)
            : this()
        {
            DatabaseName = databaseName;
            FileName = fileName;
        }

        /* Static methods for convenience */
        public static void Restore(string databaseName, string fileName) =>
            new RestoreDatabaseTask(databaseName, fileName).Execute();

        /* Implementation & stuff */
        private static string DefaultDataPathSql =>
            "SELECT CAST(serverproperty('InstanceDefaultDataPath') AS NVARCHAR(1000)) AS DefaultDataPath";

        private string FileListSql =>
            $@"USE [master]
RESTORE FILELISTONLY FROM DISK=N'{Path.GetFullPath(FileName)}'";

        private List<BackupFile> FileList { get; set; }

        private sealed class BackupFile
        {
            internal string LogicalName { get; set; }
            internal long FileId { get; set; }
            internal string Type { get; set; }
            internal string Suffix
            {
                get
                {
                    if (Type == "D")
                        return FileId > 1 ? $"_{FileId}.ndf" : ".mdf";
                    return FileId > 1 ? $"_{FileId}.log" : ".log";
                }
            }
        }

        private BackupFile CurrentBackupFile { get; set; }
        private string DefaultDataPath { get; set; }
    }
}
