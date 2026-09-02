using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// A sequence is a shortcute for custom task, but with the TaskType "SEQUENCE".
    /// </summary>
    [PublicAPI]
    public class Sequence : GenericTask
    {
        public sealed override string TaskName { get; set; } = "Sequence";

        public void Execute() =>
            new CustomTask(TaskName) { TaskType = TaskType, TaskHash = TaskHash }.Execute(Tasks);

        public Action Tasks { get; set; }

        public Sequence() { }

        public Sequence(string name)
            : this()
        {
            TaskName = name;
        }

        public Sequence(string name, Action tasks)
            : this(name)
        {
            Tasks = tasks;
        }

        public static void Execute(string name, Action tasks) =>
            new Sequence(name, tasks).Execute();
    }

    /// <summary>
    /// A <see cref="Sequence"/> whose tasks receive a <typeparamref name="T"/> parent object, for
    /// grouping tasks that all operate on the same context object.
    /// </summary>
    /// <typeparam name="T">Type of the parent object passed to <see cref="Tasks"/>.</typeparam>
    [PublicAPI]
    public class Sequence<T> : Sequence
    {
        /// <summary>
        /// The context object passed to <see cref="Tasks"/>.
        /// </summary>
        public T Parent { get; set; }

        /// <summary>
        /// The action to run, receiving <see cref="Parent"/>. Shadows <see cref="Sequence.Tasks"/>,
        /// which takes no parameter.
        /// </summary>
        public new Action<T> Tasks { get; set; }

        /// <summary>
        /// Creates a new instance with no name, tasks, or parent set yet.
        /// </summary>
        public Sequence() { }

        /// <summary>
        /// Creates a new instance with the given task name.
        /// </summary>
        /// <param name="name">Task name.</param>
        public Sequence(string name)
            : base(name) { }

        /// <summary>
        /// Creates a new instance with the given task name, tasks, and parent object.
        /// </summary>
        /// <param name="name">Task name.</param>
        /// <param name="tasks">The action to run, receiving <paramref name="parent"/>.</param>
        /// <param name="parent">The context object passed to <paramref name="tasks"/>.</param>
        public Sequence(string name, Action<T> tasks, T parent)
            : base(name)
        {
            Tasks = tasks;
            Parent = parent;
        }

        /// <summary>
        /// Runs <see cref="Tasks"/> with <see cref="Parent"/>, logged as a task of type
        /// <c>"SEQUENCE"</c>. Shadows <see cref="Sequence.Execute()"/>.
        /// </summary>
        public new void Execute() =>
            new CustomTask(TaskName) { TaskType = TaskType, TaskHash = TaskHash }.Execute(
                Tasks,
                Parent
            );

        /// <summary>
        /// Creates and runs a <see cref="Sequence{T}"/> in one call.
        /// </summary>
        /// <param name="name">Task name.</param>
        /// <param name="tasks">The action to run, receiving <paramref name="parent"/>.</param>
        /// <param name="parent">The context object passed to <paramref name="tasks"/>.</param>
        public static void Execute(string name, Action<T> tasks, T parent) =>
            new Sequence<T>(name, tasks, parent).Execute();
    }
}
