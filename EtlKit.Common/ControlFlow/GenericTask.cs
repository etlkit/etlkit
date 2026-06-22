using System.Globalization;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.ControlFlow
{
    [PublicAPI]
    public abstract class GenericTask : ITask
    {
        private string _taskType;
        public virtual string TaskType
        {
            get => string.IsNullOrEmpty(_taskType) ? GetType().Name : _taskType;
            set => _taskType = value;
        }
        public virtual string TaskName { get; set; } = "N/A";

        [CanBeNull]
        private ILogger _logger;

        /// <summary>
        /// Logger instance. When injected via constructor, the injected logger is used.
        /// Otherwise falls back to <see cref="ControlFlow.LoggerFactory"/>.
        /// </summary>
        public ILogger Logger => _logger ??= ControlFlow.LoggerFactory.CreateLogger<GenericTask>();

        /// <summary>
        /// Creates a new instance with no logger (uses static LoggerFactory fallback).
        /// </summary>
        protected GenericTask() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        /// <param name="logger">Optional logger instance. If null, falls back to static LoggerFactory.</param>
        protected GenericTask([CanBeNull] ILogger logger)
        {
            _logger = logger;
        }

        public IConnectionManager ConnectionManager
        {
            get => _connectionManager;
            set
            {
                _connectionManager = value;
                OnConnectionManagerChanged(value);
            }
        }

        protected virtual void OnConnectionManagerChanged(IConnectionManager value) { }

        internal virtual IConnectionManager DbConnectionManager =>
            ConnectionManager ?? ControlFlow.DefaultDbConnection;

        public ConnectionManagerType ConnectionType => DbConnectionManager.ConnectionManagerType;
        public string QB => DbConnectionManager.QB;
        public string QE => DbConnectionManager.QE;

        private bool _disableLogging;
        public virtual bool DisableLogging
        {
            get => ControlFlow.DisableAllLogging || _disableLogging;
            set => _disableLogging = value;
        }

        public virtual CultureInfo CurrentCulture => ConnectionManager?.ConnectionCulture;

        private string _taskHash;
        private IConnectionManager _connectionManager;

        public virtual string TaskHash
        {
            get => _taskHash ?? HashHelper.Encrypt_Char40(this);
            set => _taskHash = value;
        }
        internal virtual bool HasName => !string.IsNullOrWhiteSpace(TaskName);

        public void CopyTaskProperties(ITask otherTask)
        {
            TaskName = otherTask.TaskName;
            TaskHash = otherTask.TaskHash;
            TaskType = otherTask.TaskType;
            ConnectionManager = otherTask.ConnectionManager;
            DisableLogging = otherTask.DisableLogging;
        }
    }
}
