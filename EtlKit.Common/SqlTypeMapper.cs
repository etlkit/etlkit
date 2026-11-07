using System;
using System.Data;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace EtlKit.Common
{
    /// <summary>
    /// Driver-independent mapping between SQL type names, .NET types, and <see cref="DbType"/>, plus
    /// helpers for <c>char(N)</c>-style type definitions. These are pure functions: nothing here
    /// depends on a database engine or on a table definition.
    /// </summary>
    /// <remarks>
    /// Engine-specific rewriting of a column's SQL type (for example SQL Server's <c>TEXT</c>
    /// becoming <c>VARCHAR(MAX)</c>) is deliberately not part of this class; it stays with the
    /// connection managers, see <c>docs/tech-debt/TECH-DEBT-DataTypeConverter-Driver-Split.md</c>.
    /// </remarks>
    [PublicAPI]
    public static class SqlTypeMapper
    {
        /// <summary>
        /// Default display length for a tiny integer column.
        /// </summary>
        public const int DefaultTinyIntegerLength = 5;

        /// <summary>
        /// Default display length for a small integer column.
        /// </summary>
        public const int DefaultSmallIntegerLength = 7;

        /// <summary>
        /// Default display length for an integer column.
        /// </summary>
        public const int DefaultIntegerLength = 11;

        /// <summary>
        /// Default display length for a big integer column.
        /// </summary>
        public const int DefaultBigIntegerLength = 21;

        /// <summary>
        /// Default display length for a <c>datetime2</c> column.
        /// </summary>
        public const int DefaultDateTime2Length = 41;

        /// <summary>
        /// Default display length for a <c>datetime</c> column.
        /// </summary>
        public const int DefaultDateTimeLength = 27;

        /// <summary>
        /// Default display length for a decimal column.
        /// </summary>
        public const int DefaultDecimalLength = 41;

        /// <summary>
        /// Default length used for character-type columns when none can be determined, and as the
        /// <see cref="GetStringLengthFromCharString"/> fallback.
        /// </summary>
        public const int DefaultStringLength = 255;

        private const string CharTypeDefinitionRegex = @"(.*?)char\((\d*)\)(.*?)";

        /// <summary>
        /// Whether <paramref name="value"/> is a <c>char(N)</c>/<c>varchar(N)</c>/<c>nchar(N)</c>-style
        /// type definition with an explicit length.
        /// </summary>
        /// <param name="value">A SQL type definition string.</param>
        public static bool IsCharTypeDefinition(string value) =>
            new Regex(CharTypeDefinitionRegex, RegexOptions.IgnoreCase).IsMatch(value);

        /// <summary>
        /// Extracts the length from a <c>char(N)</c>-style type definition matched by <see
        /// cref="IsCharTypeDefinition"/>, or <see cref="DefaultStringLength"/> if none can be parsed.
        /// </summary>
        /// <param name="value">A SQL type definition string, e.g. <c>"varchar(50)"</c>.</param>
        public static int GetStringLengthFromCharString(string value)
        {
            var possibleResult = Regex.Replace(
                value,
                CharTypeDefinitionRegex,
                "${2}",
                RegexOptions.IgnoreCase
            );
            return int.TryParse(possibleResult, out var result) ? result : DefaultStringLength;
        }

        /// <summary>
        /// Maps a SQL type name (optionally with a length/precision suffix, e.g. <c>"varchar(50)"</c>)
        /// to the fully-qualified name of its .NET equivalent. Unrecognized types map to <c>"System.String"</c>.
        /// </summary>
        /// <param name="dbSpecificTypeName">A SQL type name, e.g. <c>"int"</c>, <c>"datetime2"</c>.</param>
        public static string GetNETObjectTypeString(string dbSpecificTypeName)
        {
            if (dbSpecificTypeName.IndexOf("(", StringComparison.Ordinal) >= 1)
                dbSpecificTypeName = dbSpecificTypeName.Substring(
                    0,
                    dbSpecificTypeName.IndexOf("(", StringComparison.Ordinal)
                );
            dbSpecificTypeName = dbSpecificTypeName.Trim().ToLower();
            return dbSpecificTypeName switch
            {
                "bit" => "System.Boolean",
                "boolean" => "System.Boolean",
                "tinyint" => "System.UInt16",
                "smallint" => "System.Int16",
                "int2" => "System.Int16",
                "int" => "System.Int32",
                "int4" => "System.Int32",
                "int8" => "System.Int32",
                "integer" => "System.Int32",
                "bigint" => "System.Int64",
                "decimal" => "System.Decimal",
                "number" => "System.Decimal",
                "money" => "System.Decimal",
                "smallmoney" => "System.Decimal",
                "numeric" => "System.Decimal",
                "real" => "System.Double",
                "float" => "System.Double",
                "float4" => "System.Double",
                "float8" => "System.Double",
                "double" => "System.Double",
                "double precision" => "System.Double",
                "date" => "System.DateTime",
                "datetime" => "System.DateTime",
                "smalldatetime" => "System.DateTime",
                "datetime2" => "System.DateTime",
                "time" => "System.DateTime",
                "timetz" => "System.DateTime",
                "timestamp" => "System.DateTime",
                "timestamptz" => "System.DateTime",
                "uniqueidentifier" => "System.Guid",
                "uuid" => "System.Guid",
                _ => "System.String",
            };
        }

        /// <summary>
        /// Resolves a SQL type name to its .NET <see cref="Type"/>, via <see cref="GetNETObjectTypeString"/>.
        /// </summary>
        /// <param name="dbSpecificTypeName">A SQL type name, e.g. <c>"int"</c>, <c>"datetime2"</c>.</param>
        public static Type GetTypeObject(string dbSpecificTypeName)
        {
            return Type.GetType(GetNETObjectTypeString(dbSpecificTypeName));
        }

        /// <summary>
        /// Resolves a SQL type name to the matching <see cref="System.Data.DbType"/>, via <see
        /// cref="GetNETObjectTypeString"/>. Falls back to <see cref="DbType.String"/> if the mapped
        /// .NET type name has no matching <see cref="System.Data.DbType"/> value.
        /// </summary>
        /// <param name="dbSpecificTypeName">A SQL type name, e.g. <c>"int"</c>, <c>"datetime2"</c>.</param>
        public static DbType GetDBType(string dbSpecificTypeName)
        {
            try
            {
                return (DbType)
                    Enum.Parse(
                        typeof(DbType),
                        GetNETObjectTypeString(dbSpecificTypeName).Replace("System.", ""),
                        true
                    );
            }
            catch
            {
                return DbType.String;
            }
        }

        /// <summary>
        /// Returns the <see cref="System.DateTimeKind"/> implied by a SQL date/time type name — <see
        /// cref="DateTimeKind.Utc"/> for timezone-aware types, <see cref="DateTimeKind.Unspecified"/>
        /// for the rest, or <see langword="null"/> for non-date/time types.
        /// </summary>
        /// <param name="dbSpecificTypeName">A SQL type name, e.g. <c>"datetime2"</c>, <c>"timestamptz"</c>.</param>
        public static DateTimeKind? GetNETDateTimeKind(string dbSpecificTypeName)
        {
            return dbSpecificTypeName switch
            {
                "date" => DateTimeKind.Unspecified,
                "datetime" => DateTimeKind.Unspecified,
                "smalldatetime" => DateTimeKind.Unspecified,
                "datetime2" => DateTimeKind.Unspecified,
                "time" => DateTimeKind.Unspecified,
                "timestamp" => DateTimeKind.Unspecified,
                "timetz" => DateTimeKind.Utc,
                "timestamptz" => DateTimeKind.Utc,
                _ => null,
            };
        }
    }
}
