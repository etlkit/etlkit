using System.Globalization;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace EtlKit.Common.ControlFlow
{
    /// <summary>
    /// Base implementation of <see cref="ITask"/> shared by all control flow tasks and data flow
    /// components: task identity, logging settings, and connection manager access.
    /// </summary>
    [PublicAPI]
    public abstract class GenericTask : ITask
    {
        private string _taskType;

        /// <inheritdoc />
        /// <remarks>
        /// Defaults to the runtime type name (<c>GetType().Name</c>) until explicitly set.
        /// </remarks>
        public virtual string TaskType
        {
            get => string.IsNullOrEmpty(_taskType) ? GetType().Name : _taskType;
            set => _taskType = value;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Defaults to <c>"N/A"</c> until explicitly set.
        /// </remarks>
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

        /// <inheritdoc />
        /// <remarks>
        /// Setting this property invokes <see cref="OnConnectionManagerChanged"/>.
        /// </remarks>
        public IConnectionManager ConnectionManager
        {
            get => _connectionManager;
            set
            {
                _connectionManager = value;
                OnConnectionManagerChanged(value);
            }
        }

        /// <summary>
        /// Called whenever <see cref="ConnectionManager"/> is set, including to <see langword="null"/>.
        /// Does nothing by default; derived classes override to react to the change.
        /// </summary>
        /// <param name="value">The newly assigned connection manager.</param>
        protected virtual void OnConnectionManagerChanged(IConnectionManager value) { }

        /// <summary>
        /// The connection manager this task actually uses: <see cref="ConnectionManager"/> if set,
        /// otherwise <see cref="ControlFlow.DefaultDbConnection"/>.
        /// </summary>
        internal virtual IConnectionManager DbConnectionManager =>
            ConnectionManager ?? ControlFlow.DefaultDbConnection;

        /// <summary>
        /// The database engine of <see cref="DbConnectionManager"/>.
        /// </summary>
        public ConnectionManagerType ConnectionType => DbConnectionManager.ConnectionManagerType;

        /// <summary>
        /// Quotation begin character used to escape identifiers, from <see cref="DbConnectionManager"/>.
        /// </summary>
        public string QB => DbConnectionManager.QB;

        /// <summary>
        /// Quotation end character used to escape identifiers, from <see cref="DbConnectionManager"/>.
        /// </summary>
        public string QE => DbConnectionManager.QE;

        private bool _disableLogging;

        /// <inheritdoc />
        /// <remarks>
        /// Also <see langword="true"/> whenever the global <see cref="ControlFlow.DisableAllLogging"/>
        /// switch is set, regardless of this instance's own value.
        /// </remarks>
        public virtual bool DisableLogging
        {
            get => ControlFlow.DisableAllLogging || _disableLogging;
            set => _disableLogging = value;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Sourced from <see cref="ConnectionManager"/>; <see langword="null"/> if no connection
        /// manager has been assigned.
        /// </remarks>
        public virtual CultureInfo CurrentCulture => ConnectionManager?.ConnectionCulture;

        private string _taskHash;
        private IConnectionManager _connectionManager;

        /// <inheritdoc />
        /// <remarks>
        /// Computed lazily via <see cref="HashHelper.Encrypt_Char40"/> from this instance until
        /// explicitly set.
        /// </remarks>
        public virtual string TaskHash
        {
            get => _taskHash ?? HashHelper.Encrypt_Char40(this);
            set => _taskHash = value;
        }

        /// <summary>
        /// Whether <see cref="TaskName"/> has been set to a non-blank value.
        /// </summary>
        internal virtual bool HasName => !string.IsNullOrWhiteSpace(TaskName);

        /// <summary>
        /// Copies identity and logging settings (<see cref="ITask.TaskName"/>, <see
        /// cref="ITask.TaskHash"/>, <see cref="ITask.TaskType"/>, <see cref="ITask.ConnectionManager"/>,
        /// <see cref="ITask.DisableLogging"/>) from <paramref name="otherTask"/> onto this instance.
        /// </summary>
        /// <param name="otherTask">The task to copy properties from.</param>
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
