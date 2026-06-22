using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    /// <summary>
    /// A package is a shortcute for custom task, but with the TaskType "PACKAGE".
    /// </summary>
    [PublicAPI]
    public class Package : GenericTask
    {
        public sealed override string TaskName { get; set; } = "Package";

        public void Execute() =>
            new CustomTask(TaskName) { TaskType = TaskType, TaskHash = TaskHash }.Execute(Tasks);

        public Action Tasks { get; set; }

        public Package() { }

        public Package(string name)
            : this()
        {
            TaskName = name;
        }

        public Package(string name, Action tasks)
            : this(name)
        {
            Tasks = tasks;
        }

        public static void Execute(string name, Action tasks) => new Package(name, tasks).Execute();
    }
}
