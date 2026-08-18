namespace EtlKit.Primitives
{
    /// <summary>
    /// Identifies the database engine an <see cref="IConnectionManager"/> connects to, used to select
    /// driver-specific SQL conventions and type mappings.
    /// </summary>
    public enum ConnectionManagerType
    {
        /// <summary>
        /// No specific database engine; used for connection managers that do not map to one of the
        /// other known types.
        /// </summary>
        Unknown,

        /// <summary>
        /// Microsoft SQL Server.
        /// </summary>
        SqlServer,

        /// <summary>
        /// SQL Server Analysis Services (OLAP/XMLA), accessed via ADOMD.NET.
        /// </summary>
        Adomd,

        /// <summary>
        /// SQLite.
        /// </summary>
        SQLite,

        /// <summary>
        /// MySQL.
        /// </summary>
        MySql,

        /// <summary>
        /// PostgreSQL.
        /// </summary>
        Postgres,

        /// <summary>
        /// Microsoft Access, accessed via ODBC.
        /// </summary>
        Access,

        /// <summary>
        /// ClickHouse.
        /// </summary>
        ClickHouse,
    }
}
