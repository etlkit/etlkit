using EtlKit.Common;
using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    [PublicAPI]
    public abstract class DropTask<T> : GenericTask
        where T : IfExistsTask, new()
    {
        public override string TaskName => $"Drop Object {ObjectName}";

        public void Execute()
        {
            var objectExists = new T
            {
                ObjectName = ObjectName,
                OnObjectName = OnObjectName,
                ConnectionManager = ConnectionManager,
                DisableLogging = true
            }.Exists();
            if (objectExists)
                new SqlTask(this, Sql).ExecuteNonQuery();
        }

        public string ObjectName { get; set; }
        public ObjectNameDescriptor ON => new(ObjectName, QB, QE);
        internal string OnObjectName { get; set; }
        public string Sql => GetSql();

        internal virtual string GetSql() => string.Empty;

        public void Drop() => new SqlTask(this, Sql).ExecuteNonQuery();

        public void DropIfExists() => Execute();
    }
}
