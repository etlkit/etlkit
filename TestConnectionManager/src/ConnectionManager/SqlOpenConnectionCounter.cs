using EtlKit;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using Microsoft.Data.SqlClient;

namespace EtlKit.TestConnectionManager.ConnectionManager
{
    /// <summary>
    /// Надёжный подсчёт открытых соединений SQL Server для тестов.
    /// </summary>
    /// <remarks>
    /// Прежняя реализация считала соединения <b>по всей БД</b>
    /// (<c>sys.sysprocesses WHERE DB_NAME(dbid) = '{db}'</c>), из-за чего тесты были нестабильны
    /// при параллельном прогоне нескольких сборок против общего внешнего SQL Server: в подсчёт
    /// попадали соединения других тестов к той же БД.
    /// <para>
    /// Здесь подсчёт изолирован строго соединениями конкретного теста: каждое тестовое соединение
    /// помечается уникальным <c>Application Name</c> (<see cref="NewApplicationName"/>), а счёт
    /// ведётся по <c>sys.dm_exec_sessions</c> с фильтром по этому имени. Считающее (probe)
    /// соединение использует отдельное имя приложения, отключённый пул и БД <c>master</c>, поэтому
    /// само в подсчёт не попадает и не остаётся в пуле.
    /// </para>
    /// </remarks>
    internal static class SqlOpenConnectionCounter
    {
        /// <summary>
        /// Создаёт уникальное (на каждый запуск) имя приложения для пометки соединений теста.
        /// Уникальность гарантирует, что подсчёт не пересечётся с соединениями параллельных
        /// тестов/сборок и с «хвостами» прошлых прогонов на общем сервере.
        /// </summary>
        public static string NewApplicationName(string label) =>
            $"ETLBoxTest-{label}-{Guid.NewGuid():N}";

        /// <summary>
        /// Возвращает строку подключения с проставленным <c>Application Name</c>, чтобы соединения,
        /// открытые на её основе, можно было однозначно отличить при подсчёте.
        /// </summary>
        public static string TagConnectionString(string connectionString, string applicationName) =>
            new SqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = applicationName,
            }.ConnectionString;

        /// <summary>
        /// Считает количество серверных сессий, открытых соединениями с заданным
        /// <paramref name="applicationName"/> (исключая саму считающую сессию).
        /// </summary>
        public static int CountOpenConnections(string connectionString, string applicationName)
        {
            // Probe-соединение: отдельное имя приложения + Pooling=false + master.
            // Так оно не учитывается фильтром по applicationName и не оседает в пуле.
            var probeConnectionString = new SqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = applicationName + "-probe",
                Pooling = false,
                InitialCatalog = "master",
            }.ConnectionString;

            using var probe = new SqlConnectionManager(
                new SqlConnectionString(probeConnectionString)
            );
            return new SqlTask(
                    "Count open connections",
                    $@"SELECT COUNT(*) FROM sys.dm_exec_sessions
                    WHERE program_name = '{applicationName}' AND session_id <> @@SPID"
                )
                {
                    ConnectionManager = probe,
                    DisableLogging = true,
                }.ExecuteScalar<int>() ?? 0;
        }
    }
}
