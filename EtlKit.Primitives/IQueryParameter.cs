using System.Data;

namespace EtlKit.Primitives
{
    /// <summary>
    /// A named, typed parameter value bound to a SQL command.
    /// </summary>
    public interface IQueryParameter
    {
        /// <summary>
        /// Parameter name, without the driver-specific prefix (e.g. <c>@</c> or <c>:</c>).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The .NET or SQL type name of the parameter, used to derive <see cref="DBType"/>.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// The parameter value.
        /// </summary>
        object Value { get; }

        /// <summary>
        /// The <see cref="System.Data.DbType"/> equivalent of <see cref="Type"/>.
        /// </summary>
        DbType DBType { get; }
    }
}
