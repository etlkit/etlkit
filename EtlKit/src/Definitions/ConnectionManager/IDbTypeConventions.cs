namespace EtlKit.ConnectionManager
{
    /// <summary>
    /// Engine-specific rules for the SQL type a column is created with. A connection manager
    /// supplies its engine's rules through <see cref="DbConnectionManager{TConnection}.TypeConventions"/>.
    /// </summary>
    [PublicAPI]
    public interface IDbTypeConventions
    {
        /// <summary>
        /// Returns the SQL type to use for <paramref name="column"/> on this engine, adapting its
        /// <see cref="ITableColumn.DataType"/> to the engine's quirks (for example SQL Server's
        /// <c>TEXT</c> becoming <c>VARCHAR(MAX)</c>). Returns <see cref="ITableColumn.DataType"/>
        /// unchanged when the engine needs no adaptation.
        /// </summary>
        /// <param name="column">The column whose type to adapt.</param>
        string GetDbSpecificType(ITableColumn column);
    }
}
