using EtlKit;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using Npgsql;

namespace EtlKit.TestConnectionManager.ConnectionManager
{
    /// <summary>
    /// Reliable counting of open PostgreSQL connections for tests.
    /// </summary>
    /// <remarks>
    /// The previous implementation counted connections <b>across the whole database</b>
    /// (<c>pg_stat_activity WHERE datname = '{db}'</c>), which made tests unstable when several
    /// builds were running in parallel against a shared PostgreSQL server: connections from
    /// other tests targeting the same database were picked up by the count.
    /// <para>
    /// Here the count is isolated strictly to the connections of a single test: each test
    /// connection is tagged with a unique <c>Application Name</c> (<see cref="NewApplicationName"/>),
    /// and the count is taken from <c>pg_stat_activity</c> filtered by that name. The probe
    /// connection uses a separate application name, disabled pooling and the <c>postgres</c>
    /// database, so it is not counted by the filter and does not linger in the pool.
    /// </para>
    /// <para>
    /// This mirrors the SQL Server helper <see cref="SqlOpenConnectionCounter"/>.
    /// </para>
    /// </remarks>
    internal static class PostgresOpenConnectionCounter
    {
        /// <summary>
        /// Creates a unique (per run) application name used to tag a test's connections.
        /// Uniqueness ensures the count does not collide with connections from parallel
        /// tests/builds or with leftovers from previous runs on a shared server.
        /// </summary>
        /// <remarks>
        /// PostgreSQL truncates <c>application_name</c> at <c>NAMEDATALEN - 1 = 63</c> bytes,
        /// so an over-long name stored on the server would not match the unfiltered string used
        /// in the WHERE clause. The format below caps the final name at 57 chars, leaving 6
        /// bytes of headroom for the <c>-probe</c> suffix the counter appends so the probe's
        /// stored name also survives without truncation.
        /// </remarks>
        public static string NewApplicationName(string label)
        {
            const int MaxLen = 57;
            const string Prefix = "ETB-";
            var id = Guid.NewGuid().ToString("N").Substring(0, 16);
            var maxLabelLen = MaxLen - Prefix.Length - 1 - id.Length;
            var safeLabel = label.Length > maxLabelLen ? label.Substring(0, maxLabelLen) : label;
            return $"{Prefix}{safeLabel}-{id}";
        }

        /// <summary>
        /// Returns a connection string with <c>Application Name</c> set so that connections
        /// opened from it can be uniquely identified during counting.
        /// </summary>
        public static string TagConnectionString(string connectionString, string applicationName) =>
            new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = applicationName,
            }.ConnectionString;

        /// <summary>
        /// Returns the number of server sessions opened by connections that carry the given
        /// <paramref name="applicationName"/> (excluding the probe session itself).
        /// </summary>
        public static int CountOpenConnections(string connectionString, string applicationName)
        {
            // Probe connection: distinct application name + Pooling=false + master database.
            // This way it is not matched by the applicationName filter and does not stay in the pool.
            var probeConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = applicationName + "-probe",
                Pooling = false,
                Database = "postgres",
            }.ConnectionString;

            using var probe = new PostgresConnectionManager(
                new PostgresConnectionString(probeConnectionString)
            );
            return new SqlTask(
                    "Count open connections",
                    $@"SELECT COUNT(*) FROM pg_stat_activity
                    WHERE application_name = '{applicationName}' AND pid <> pg_backend_pid()"
                )
                {
                    ConnectionManager = probe,
                    DisableLogging = true,
                }.ExecuteScalar<int>() ?? 0;
        }
    }
}
