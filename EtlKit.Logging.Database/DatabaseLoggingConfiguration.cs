using System;
using System.Diagnostics.CodeAnalysis;
using EtlKit.Common.Logging;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;

namespace EtlKit.Logging.Database
{
    [PublicAPI]
    public static class DatabaseLoggingConfiguration
    {
        public const string DefaultLogTableName = "etlkit_log";

        /// <summary>
        /// TableName of the current log process logging table
        /// </summary>
        public static string LogTable { get; set; } = DefaultLogTableName;

        /// <summary>
        /// If you used the logging task StartLoadProcess (and created the corresponding load process table before)
        /// then this Property will hold the current load process information.
        /// </summary>
        public static LoadProcess CurrentLoadProcess { get; internal set; }

        public const string DefaultLoadProcessTableName = "etlkit_loadprocess";

        public static void AddDatabaseLoggingConfiguration(IConnectionManager connection) =>
            AddDatabaseLoggingConfiguration(connection, LogLevel.Information, LogTable);

        [SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "DatabaseTarget is passed to NLog configuration which takes ownership and manages its lifecycle"
        )]
        public static void AddDatabaseLoggingConfiguration(
            IConnectionManager connectionManager,
            LogLevel minLogLevel,
            string tableName
        )
        {
            if (
                LogTable != null
                && LogTable != DefaultLoadProcessTableName
                && tableName == DefaultLoadProcessTableName
            )
            {
                tableName = LogTable;
            }

            // CA2000: DatabaseTarget is passed to NLog configuration which takes ownership of the object
            // and manages its lifecycle. Disposing it here would cause issues with logging functionality.
            var newTarget = new CreateDatabaseTarget(
                connectionManager,
                tableName
            ).GetNLogDatabaseTarget();
            var config = new LoggingConfiguration();
            config.AddRule(Map(minLogLevel), NLog.LogLevel.Error, newTarget);

            Common.ControlFlow.ControlFlow.LoggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .ClearProviders()
                    .AddNLog(
                        config,
                        new NLogProviderOptions
                        {
                            IncludeScopes = true,
                            CaptureMessageParameters = true,
                            ParseMessageTemplates = true,
                        }
                    );
            });
        }

        private static NLog.LogLevel Map(LogLevel logLevel) =>
            logLevel switch
            {
                LogLevel.Trace => NLog.LogLevel.Trace,
                LogLevel.Debug => NLog.LogLevel.Debug,
                LogLevel.Information => NLog.LogLevel.Info,
                LogLevel.Warning => NLog.LogLevel.Warn,
                LogLevel.Critical => NLog.LogLevel.Error,
                LogLevel.Error => NLog.LogLevel.Fatal,
                _ => throw new NotSupportedException($"LogLevel '{logLevel}' is not supported"),
            };
    }
}
