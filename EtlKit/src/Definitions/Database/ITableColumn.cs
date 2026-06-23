namespace EtlKit
{
    public interface ITableColumn
    {
        string Name { get; }
        string DataType { get; }
        bool AllowNulls { get; }
        bool IsIdentity { get; }
        bool IsPrimaryKey { get; }
        string DefaultValue { get; }
        string Collation { get; }
        string ComputedColumn { get; }
        bool HasComputedColumn { get; }
        int? IdentitySeed { get; } //Sql Server only
        int? IdentityIncrement { get; } //Sql Server only
        string Comment { get; } //MySql only
    }
}
