namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// The conventions of an engine that needs no type adaptation: every column keeps its data type.
    /// </summary>
    internal sealed class PassThroughTypeConventions : IDbTypeConventions
    {
        public string GetDbSpecificType(ITableColumn column) => column.DataType;
    }
}
