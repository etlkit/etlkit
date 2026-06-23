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

    [PublicAPI]
    public class Sequence<T> : Sequence
    {
        public T Parent { get; set; }
        public new Action<T> Tasks { get; set; }

        public Sequence() { }

        public Sequence(string name)
            : base(name) { }

        public Sequence(string name, Action<T> tasks, T parent)
            : base(name)
        {
            Tasks = tasks;
            Parent = parent;
        }

        public new void Execute() =>
            new CustomTask(TaskName) { TaskType = TaskType, TaskHash = TaskHash }.Execute(
                Tasks,
                Parent
            );

        public static void Execute(string name, Action<T> tasks, T parent) =>
            new Sequence<T>(name, tasks, parent).Execute();
    }
}
