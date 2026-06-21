using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using EtlKit;
using EtlKit.ClickHouse.ConnectionManager;
using EtlKit.ClickHouse.ConnectionStrings;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using Microsoft.Extensions.Configuration;

namespace EtlKit.TestShared.Helper;

public static class Config
{
    private static IConfigurationRoot s_defaultConfigFile;

    public static ConnectionDetails<
        SqlConnectionString,
        SqlConnectionManager
    > SqlConnection { get; } = new("SqlConnectionString");

    public static ConnectionDetails<
        SqlConnectionString,
        AdomdConnectionManager
    > SSASConnection { get; } = new("SSASConnectionString");

    public static SQLiteConnectionDetails SQLiteConnection { get; } = new("SQLiteConnectionString");

    public static ConnectionDetails<
        MySqlConnectionString,
        MySqlConnectionManager
    > MySqlConnection { get; } = new("MySqlConnectionString");

    public static ConnectionDetails<
        PostgresConnectionString,
        PostgresConnectionManager
    > PostgresConnection { get; } = new("PostgresConnectionString");

    public static ConnectionDetails<
        ClickHouseConnectionString,
        ClickHouseConnectionManager
    > ClickHouseConnection { get; } = new("ClickHouseConnectionString");

    public static ConnectionDetails<
        OdbcConnectionString,
        AccessOdbcConnectionManager
    > AccessOdbcConnection { get; } = new("AccessOdbcConnectionString");

    public static ConnectionDetails<
        OdbcConnectionString,
        SqlOdbcConnectionManager
    > SqlOdbcConnection { get; } = new("SqlOdbcConnectionString");

    public static ConnectionDetails<
        SqlConnectionString,
        SqlConnectionManager
    > AzureSqlConnection { get; } = new("AzureSqlConnectionString");

    internal static IConfigurationRoot DefaultConfigFile
    {
        get
        {
            if (s_defaultConfigFile != null)
            {
                return s_defaultConfigFile;
            }

            var environmentVariable = Environment.GetEnvironmentVariable("EtlKitConfig");
            var path = string.IsNullOrWhiteSpace(environmentVariable)
                ? "default.config.json"
                : environmentVariable;
            Load(path);

            return s_defaultConfigFile;
        }
        private set => s_defaultConfigFile = value;
    }

    public static IEnumerable<IConnectionManager> AllSqlConnections(string section)
    {
        yield return ClickHouseConnection.ConnectionManager(section);
        yield return PostgresConnection.ConnectionManager(section);
        yield return MySqlConnection.ConnectionManager(section);
        yield return SqlConnection.ConnectionManager(section);
        yield return SQLiteConnection.ConnectionManager(section);
    }

    public static IEnumerable<IConnectionManager> AllConnectionsWithoutSQLite(string section)
#pragma warning restore S4144
    {
        yield return ClickHouseConnection.ConnectionManager(section);
        yield return PostgresConnection.ConnectionManager(section);
        yield return MySqlConnection.ConnectionManager(section);
        yield return SqlConnection.ConnectionManager(section);
    }

    public static IEnumerable<IConnectionManager> AllConnectionsWithoutClickHouse(string section)
    {
        yield return PostgresConnection.ConnectionManager(section);
        yield return MySqlConnection.ConnectionManager(section);
        yield return SqlConnection.ConnectionManager(section);
        yield return SQLiteConnection.ConnectionManager(section);
    }

    public static IEnumerable<IConnectionManager> AllConnectionsWithoutSQLiteAndClickHouse(
        string section
    )
    {
        yield return SqlConnection.ConnectionManager(section);
        yield return PostgresConnection.ConnectionManager(section);
        yield return MySqlConnection.ConnectionManager(section);
    }

    public static IEnumerable<IConnectionManager> AllOdbcConnections(string section)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield break;
        }

        yield return SqlOdbcConnection.ConnectionManager(section);
        yield return AccessOdbcConnection.ConnectionManager(section);
    }

    public static IEnumerable<IConnectionManager> AccessConnection(string section)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return AccessOdbcConnection.ConnectionManager(section);
        }
    }

    private static void Load(string configFile)
    {
        DefaultConfigFile = new ConfigurationBuilder().AddJsonFile(configFile).Build();
    }

    public static IEnumerable<CultureInfo> AllLocalCultures()
    {
        return new[] { CultureInfo.GetCultureInfo("ru-RU"), CultureInfo.GetCultureInfo("en-US") };
    }

    public static string KafkaBootstrapAddress =>
        DefaultConfigFile.GetSection("Kafka")["BootstrapAddress"];
}
