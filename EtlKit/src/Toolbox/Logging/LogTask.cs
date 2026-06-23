using System.Diagnostics.CodeAnalysis;

using EtlKit.Primitives;

using EtlKit.Common.ControlFlow;

namespace EtlKit.Logging
{
    /// <summary>
    /// Used this task for custom log messages.
    /// </summary>
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
    public class LogTask : GenericTask
    {
        /* ITask Interface */
        public override string TaskName => "Logs message";

        public void Execute()
        {
            Info(Message);
        }

        /* Public properties */
        public string Message { get; set; }

        public LogTask() { }

        public LogTask(string message)
            : this()
        {
            Message = message;
        }

        public static void Trace(string message) => new LogTask(message).Trace();

        public static void Trace(IConnectionManager connectionManager, string message) =>
            new LogTask(message) { ConnectionManager = connectionManager }.Trace();

        public void Trace() =>
            Logger?.Trace(
                Message,
                TaskType,
                "LOG",
                TaskHash,
                Common.ControlFlow.ControlFlow.Stage,
                Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );

        public static void Debug(string message) => new LogTask(message).Debug();

        public static void Debug(IConnectionManager connectionManager, string message) =>
            new LogTask(message) { ConnectionManager = connectionManager }.Debug();

        public void Debug() =>
            Logger?.Debug(
                Message,
                TaskType,
                "LOG",
                TaskHash,
                Common.ControlFlow.ControlFlow.Stage,
                Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );

        public static void Info(string message) => new LogTask(message).Info();

        public static void Info(IConnectionManager connectionManager, string message) =>
            new LogTask(message) { ConnectionManager = connectionManager }.Info();

        public void Info() =>
            Logger?.Info(
                Message,
                TaskType,
                "LOG",
                TaskHash,
                Common.ControlFlow.ControlFlow.Stage,
                Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );

        public static void Warn(string message) => new LogTask(message).Warn();

        public static void Warn(IConnectionManager connectionManager, string message) =>
            new LogTask(message) { ConnectionManager = connectionManager }.Warn();

        public void Warn() =>
            Logger?.Warn(
                Message,
                TaskType,
                "LOG",
                TaskHash,
                Common.ControlFlow.ControlFlow.Stage,
                Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );

        public static void Error(string message) => new LogTask(message).Error();

        public static void Error(IConnectionManager connectionManager, string message) =>
            new LogTask(message) { ConnectionManager = connectionManager }.Error();

        public void Error() =>
            Logger?.Error(
                Message,
                TaskType,
                "LOG",
                TaskHash,
                Common.ControlFlow.ControlFlow.Stage,
                Common.ControlFlow.ControlFlow.CurrentLoadProcess?.Id
            );
    }
}
