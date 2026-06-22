using Microsoft.Data.Sqlite;

namespace EtlKit.ConnectionManager.Helpers;

/// <summary>
/// This base class provides datatype conversion services for the Sqlite provider.
/// </summary>
[PublicAPI]
public abstract class SqliteConvert
{
    private static readonly DbType[] s_dbTypeMappings = new[]
    {
        DbType.Object, // Empty (0)
        DbType.Binary, // Object (1)
        DbType.Object, // DBNull (2)
        DbType.Boolean, // Boolean (3)
        DbType.SByte, // Char (4)
        DbType.SByte, // SByte (5)
        DbType.Byte, // Byte (6)
        DbType.Int16, // Int16 (7)
        DbType.UInt16, // UInt16 (8)
        DbType.Int32, // Int32 (9)
        DbType.UInt32, // UInt32 (10)
        DbType.Int64, // Int64 (11)
        DbType.UInt64, // UInt64 (12)
        DbType.Single, // Single (13)
        DbType.Double, // Double (14)
        DbType.Decimal, // Decimal (15)
        DbType.DateTime, // DateTime (16)
        DbType.Object, // ?? (17)
        DbType.String // String (18)
    };

    /// <summary>
    /// For a given intrinsic type, return a DbType
    /// </summary>
    /// <param name="objValue">The native type to convert</param>
    /// <returns>The corresponding (closest match) DbType</returns>
    public static DbType TypeToDbType(object objValue)
    {
        if (objValue == null || objValue == DBNull.Value)
        {
            return DbType.String; // Unassigned default value is String
        }

        var tc = Type.GetTypeCode(objValue.GetType());
        if (tc != TypeCode.Object)
        {
            return s_dbTypeMappings[(int)tc];
        }

        return objValue switch
        {
            byte[] => DbType.Binary,
            Guid => DbType.Guid,
            _ => DbType.String
        };
    }

    public static SqliteType TypeToAffinity(object objValue)
    {
        if (objValue != null && objValue != DBNull.Value)
        {
            return TypeToAffinity(objValue.GetType());
        }

        return SqliteType.Text; // Unassigned default value is String
    }

    /// <summary>
    /// For a given type, return the closest-match SQLite TypeAffinity, which only understands a very limited subset of types.
    /// </summary>
    /// <param name="type">The type to evaluate</param>
    /// <returns>The SQLite type affinity for that type.</returns>
    public static SqliteType TypeToAffinity(Type type)
    {
        var tc = Type.GetTypeCode(type);
        if (tc == TypeCode.Object)
        {
            if (type == typeof(byte[]) || type == typeof(Guid))
                return SqliteType.Blob;
            return SqliteType.Text;
        }

        return s_typeCodeAffinities[(int)tc];
    }

    private static readonly SqliteType[] s_typeCodeAffinities =
    {
        SqliteType.Text, // Empty (0)
        SqliteType.Blob, // Object (1)
        SqliteType.Text, // DBNull (2)
        SqliteType.Integer, // Boolean (3)
        SqliteType.Integer, // Char (4)
        SqliteType.Integer, // SByte (5)
        SqliteType.Integer, // Byte (6)
        SqliteType.Integer, // Int16 (7)
        SqliteType.Integer, // UInt16 (8)
        SqliteType.Integer, // Int32 (9)
        SqliteType.Integer, // UInt32 (10)
        SqliteType.Integer, // Int64 (11)
        SqliteType.Integer, // UInt64 (12)
        SqliteType.Real, // Single (13)
        SqliteType.Real, // Double (14)
        SqliteType.Real, // Decimal (15)
        SqliteType.Text, // DateTime (16)
        SqliteType.Text, // ?? (17)
        SqliteType.Text // String (18)
    };
}
