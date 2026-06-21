using System;
using System.Collections;
using System.Collections.Generic;
using EtlKit.Common.Logging;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtlKit.Common.ControlFlow
{
    /// <summary>
    /// Contains static information which affects all EtlKit tasks.
    /// Here you can set default connections string, disbale the logging for all processes or set the current stage used in your logging configuration.
    /// </summary>
    [PublicAPI]
    public static class ControlFlow
    {
        private static IConnectionManager s_defaultDbConnection;

        /// <summary>
        /// You can store your general database connection string here. This connection will then used by all Tasks where no DB connection is excplicitly set.
        /// </summary>
        public static IConnectionManager DefaultDbConnection
        {
            get
            {
                return s_defaultDbConnection
                    ?? throw new EtlKitException(
                        "No connection manager found! The component or task you are "
                            + "using expected a  connection manager to connect to the database."
                            + "Either pass a connection manager or set a default connection manager within the "
                            + "ControlFlow.DefaultDbConnection property!"
                    );
            }
            set { s_defaultDbConnection = value; }
        }

        /// <summary>
        /// If set to true, nothing will be logged by any control flow task or data flow component.
        /// When switched back to false, all tasks and components will continue to log.
        /// </summary>
        public static bool DisableAllLogging { get; set; }

        /// <summary>
        /// For logging purposes only. If the stage is set, you can access the stage value in the logging configuration.
        /// </summary>
        public static string Stage { get; set; }

        /// <summary>
        /// If you used the logging task StartLoadProcess (and created the corresponding load process table before)
        /// then this Property will hold the current load process information.
        /// </summary>
        public static LoadProcess CurrentLoadProcess { get; internal set; }

        public const string DefaultLoadProcessTableName = "etlkit_loadprocess";

        /// <summary>
        /// TableName of the current load process logging table
        /// </summary>
        public static string LoadProcessTable { get; set; } = DefaultLoadProcessTableName;

        public const string DefaultLogTableName = "etlkit_log";

        /// <summary>
        /// TableName of the current log process logging table
        /// </summary>
        public static string LogTable { get; set; } = DefaultLogTableName;

        /// <summary>
        /// Фабрика для создания логгера
        /// </summary>
        public static ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

        /// <summary>
        /// Set all settings back to default (which is null or false)
        /// </summary>
        public static void ClearSettings()
        {
            DefaultDbConnection = null;
            CurrentLoadProcess = null;
            DisableAllLogging = false;
            LoadProcessTable = DefaultLoadProcessTableName;
            LogTable = DefaultLogTableName;
            Stage = null;
        }

        public static void Trace(
            this ILogger logger,
            string message,
            string type,
            string action,
            string hash,
            string stage,
            long? loadProcessKey
        ) => logger.WriteLog(LogLevel.Trace, message, type, action, hash, stage, loadProcessKey);

        public static void Debug(
            this ILogger logger,
            string message,
            string type,
            string action,
            string hash,
            string stage,
            long? loadProcessKey
        ) => logger.WriteLog(LogLevel.Debug, message, type, action, hash, stage, loadProcessKey);

        public static void Info(
            this ILogger logger,
            string message,
            string type,
            string action,
            string hash,
            string stage,
            long? loadProcessKey
        ) =>
            logger.WriteLog(
                LogLevel.Information,
                message,
                type,
                action,
                hash,
                stage,
                loadProcessKey
            );

        public static void Warn(
            this ILogger logger,
            string message,
            string type,
            string action,
            string hash,
            string stage,
            long? loadProcessKey
        ) => logger.WriteLog(LogLevel.Warning, message, type, action, hash, stage, loadProcessKey);

        public static void Error(
            this ILogger logger,
            string message,
            string type,
            string action,
            string hash,
            string stage,
            long? loadProcessKey
        ) => logger.WriteLog(LogLevel.Error, message, type, action, hash, stage, loadProcessKey);

        private static void WriteLog(
            this ILogger logger,
            LogLevel logLevel,
            string message,
            string type,
            string action,
            string hash,
            string stage,
            long? loadProcessKey
        )
        {
            logger.Log(
                logLevel,
                new EventId(0, "ETL"),
                new MyLogEvent(message)
                    .WithProperty("Type", type)
                    .WithProperty("Action", action)
                    .WithProperty("Hash", hash)
                    .WithProperty("Stage", stage)
                    .WithProperty("LoadProcessKey", loadProcessKey),
                (Exception)null,
                MyLogEvent.Formatter
            );
        }
    }

    public class MyLogEvent : IEnumerable<KeyValuePair<string, object>>
    {
        readonly List<KeyValuePair<string, object>> _properties = new();

        public string Message { get; }

        public MyLogEvent(string message)
        {
            Message = message;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _properties.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public MyLogEvent WithProperty(string name, object value)
        {
            _properties.Add(new KeyValuePair<string, object>(name, value));
            return this;
        }

        public static Func<MyLogEvent, Exception, string> Formatter { get; } = (l, _) => l.Message;
    }
}
