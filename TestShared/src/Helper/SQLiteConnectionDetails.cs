using System.IO;
using EtlKit;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using JetBrains.Annotations;

namespace EtlKit.TestShared.Helper;

/// <summary>
/// Generates new database name for each connection.
/// </summary>
public class SQLiteConnectionDetails
    : ConnectionDetails<SQLiteConnectionString, SQLiteConnectionManager>
{
    public SQLiteConnectionDetails(string connectionStringName)
        : base(connectionStringName) { }

    public SQLiteConnectionString ConnectionStringToTemplate(string section) =>
        new() { Value = RawConnectionString(section) };

    public new SQLiteConnectionString ConnectionString(string section) =>
        ConnectionString(section, null);

    public SQLiteConnectionString ConnectionString(string section, string dbNameSuffix)
    {
        var connectionString = new SQLiteConnectionString { Value = RawConnectionString(section) };
        if (dbNameSuffix != null)
        {
            connectionString.DbName = SqliteFileName(connectionString.DbName, dbNameSuffix);
        }
        return connectionString;
    }

    [MustDisposeResource]
    public SQLiteConnectionManager ConnectionManager(string section, string dbNameSuffix = null)
    {
        return new SQLiteConnectionManager
        {
            ConnectionString = ConnectionString(section, dbNameSuffix),
        };
    }

    public void CopyFromTemplate(string section, string dbNameSuffix)
    {
        var sqliteTemplateFilePath = ConnectionStringToTemplate(section).DbName;
        IDbConnectionString connectionString = ConnectionString(section);
        var sqliteFileName = SqliteFileName(connectionString.DbName, dbNameSuffix);
        if (File.Exists(sqliteFileName))
        {
            return;
        }

        // Extract the directory part from the file path
        var directoryPath = Path.GetDirectoryName(sqliteFileName);

        // Check if the directory exists
        if (!Directory.Exists(directoryPath))
        {
            // Create the directory
            Directory.CreateDirectory(directoryPath!);
        }

        File.Copy(sqliteTemplateFilePath, sqliteFileName!);
    }

    public void DeleteDatabase(string section, string dbNameSuffix)
    {
        IDbConnectionString connectionString = ConnectionString(section);
        var sqliteFileName = SqliteFileName(connectionString.DbName, dbNameSuffix);
        if (!File.Exists(sqliteFileName))
        {
            return;
        }
        // This is solution to https://stackoverflow.com/questions/8511901/system-data-sqlite-close-not-releasing-database-file
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(sqliteFileName);
    }

    private static string SqliteFileName(string baseName, string dbNameSuffix)
    {
        return dbNameSuffix != null ? $"{baseName}.{dbNameSuffix}.db" : $"{baseName}.TestRun.db";
    }
}
