namespace EtlKit.TestShared.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class MultiprocessorOnlyFactAttribute : FactAttribute
    {
        public MultiprocessorOnlyFactAttribute()
        {
            if (Environment.ProcessorCount < 2)
            {
                Skip = "Ignore: parallel tests cannot run during single processor execution";
            }
        }
    }
}
