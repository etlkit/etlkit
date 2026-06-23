using EtlKit.Common;
using EtlKit.Common.ControlFlow;

namespace EtlKit.ControlFlow
{
    [PublicAPI]
    public abstract class IfExistsTask : GenericTask
    {
        public override string TaskName => $"Check if {ObjectName} exists";

        public virtual void Execute()
        {
            if (Sql != string.Empty)
                DoesExist = new SqlTask(this, Sql).ExecuteScalarAsBool();
        }

        public string ObjectName { get; set; }
        public ObjectNameDescriptor ON => new(ObjectName, QB, QE);
        internal string OnObjectName { get; set; }
        public ObjectNameDescriptor OON => new(OnObjectName, QB, QE);
        public bool DoesExist { get; internal set; }

        public string Sql
        {
            get { return GetSql(); }
        }

        internal virtual string GetSql()
        {
            return string.Empty;
        }

        public virtual bool Exists()
        {
            Execute();
            return DoesExist;
        }
    }
}
