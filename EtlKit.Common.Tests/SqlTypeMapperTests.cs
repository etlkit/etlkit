#nullable enable
using System.Data;
using Xunit;

namespace EtlKit.Common.Tests;

/// <summary>
/// Characterization tests for <see cref="SqlTypeMapper"/>, the driver-independent SQL type mapping:
/// SQL type name to .NET type / <see cref="DbType"/>, char-type parsing and the default lengths. They
/// pin today's behavior, quirks included, so the mapping can move between assemblies without changing
/// (see docs/tech-debt/TECH-DEBT-DataTypeConverter-Driver-Split.md). A quirk pinned here is not an
/// endorsement: fixing one is a separate change that updates its expectation on purpose.
/// </summary>
public sealed class SqlTypeMapperTests
{
    [Fact]
    public void FullName_IsStable() =>
        Assert.Equal("EtlKit.Common.SqlTypeMapper", typeof(SqlTypeMapper).FullName);

    // ── default lengths ──────────────────────────────────────────────────────

    [Fact]
    public void DefaultLengthConstants_HaveExpectedValues()
    {
        Assert.Equal(5, SqlTypeMapper.DefaultTinyIntegerLength);
        Assert.Equal(7, SqlTypeMapper.DefaultSmallIntegerLength);
        Assert.Equal(11, SqlTypeMapper.DefaultIntegerLength);
        Assert.Equal(21, SqlTypeMapper.DefaultBigIntegerLength);
        Assert.Equal(41, SqlTypeMapper.DefaultDateTime2Length);
        Assert.Equal(27, SqlTypeMapper.DefaultDateTimeLength);
        Assert.Equal(41, SqlTypeMapper.DefaultDecimalLength);
        Assert.Equal(255, SqlTypeMapper.DefaultStringLength);
    }

    // ── GetNETObjectTypeString ───────────────────────────────────────────────

    // The mapping looks wrong in places (int8 is 64-bit in PostgreSQL, time carries no date,
    // real is single precision in SQL Server). It is pinned as it is today.
    [Theory]
    [InlineData("bit", "System.Boolean")]
    [InlineData("boolean", "System.Boolean")]
    [InlineData("tinyint", "System.UInt16")]
    [InlineData("smallint", "System.Int16")]
    [InlineData("int2", "System.Int16")]
    [InlineData("int", "System.Int32")]
    [InlineData("int4", "System.Int32")]
    [InlineData("int8", "System.Int32")]
    [InlineData("integer", "System.Int32")]
    [InlineData("bigint", "System.Int64")]
    [InlineData("decimal", "System.Decimal")]
    [InlineData("number", "System.Decimal")]
    [InlineData("money", "System.Decimal")]
    [InlineData("smallmoney", "System.Decimal")]
    [InlineData("numeric", "System.Decimal")]
    [InlineData("real", "System.Double")]
    [InlineData("float", "System.Double")]
    [InlineData("float4", "System.Double")]
    [InlineData("float8", "System.Double")]
    [InlineData("double", "System.Double")]
    [InlineData("double precision", "System.Double")]
    [InlineData("date", "System.DateTime")]
    [InlineData("datetime", "System.DateTime")]
    [InlineData("smalldatetime", "System.DateTime")]
    [InlineData("datetime2", "System.DateTime")]
    [InlineData("time", "System.DateTime")]
    [InlineData("timetz", "System.DateTime")]
    [InlineData("timestamp", "System.DateTime")]
    [InlineData("timestamptz", "System.DateTime")]
    [InlineData("uniqueidentifier", "System.Guid")]
    [InlineData("uuid", "System.Guid")]
    public void GetNETObjectTypeString_KnownSqlType_ReturnsDotNetTypeName(
        string sqlType,
        string expected
    ) => Assert.Equal(expected, SqlTypeMapper.GetNETObjectTypeString(sqlType));

    [Theory]
    [InlineData("INT", "System.Int32")]
    [InlineData("Int", "System.Int32")]
    [InlineData("  int  ", "System.Int32")]
    [InlineData("\tBIGINT ", "System.Int64")]
    [InlineData("DOUBLE PRECISION", "System.Double")]
    public void GetNETObjectTypeString_IgnoresCaseAndSurroundingSpaces(
        string sqlType,
        string expected
    ) => Assert.Equal(expected, SqlTypeMapper.GetNETObjectTypeString(sqlType));

    [Theory]
    [InlineData("int(11)", "System.Int32")]
    [InlineData("decimal(18,2)", "System.Decimal")]
    [InlineData("decimal (18, 2)", "System.Decimal")]
    [InlineData("NUMERIC(10,2)", "System.Decimal")]
    [InlineData("datetime2(7)", "System.DateTime")]
    [InlineData("float(53)", "System.Double")]
    [InlineData("  int(11)  ", "System.Int32")]
    [InlineData("int(11", "System.Int32")]
    [InlineData("varchar(50)", "System.String")]
    public void GetNETObjectTypeString_IgnoresLengthAndPrecisionSuffix(
        string sqlType,
        string expected
    ) => Assert.Equal(expected, SqlTypeMapper.GetNETObjectTypeString(sqlType));

    [Theory]
    [InlineData("varchar")]
    [InlineData("nvarchar(50)")]
    [InlineData("char(10)")]
    [InlineData("text")]
    [InlineData("ntext")]
    [InlineData("blob")]
    [InlineData("varbinary(max)")]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("datetimeoffset")]
    [InlineData("geography")]
    [InlineData("")]
    [InlineData("   ")]
    public void GetNETObjectTypeString_UnmappedSqlType_ReturnsString(string sqlType) =>
        Assert.Equal("System.String", SqlTypeMapper.GetNETObjectTypeString(sqlType));

    // A parenthesis at position 0 is not treated as the start of a suffix, so the whole
    // text is looked up and, not being a known name, falls back to String.
    [Fact]
    public void GetNETObjectTypeString_LeadingParenthesis_IsNotStripped() =>
        Assert.Equal("System.String", SqlTypeMapper.GetNETObjectTypeString("(int)"));

    // ── GetTypeObject ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("bit", typeof(bool))]
    [InlineData("tinyint", typeof(ushort))]
    [InlineData("smallint", typeof(short))]
    [InlineData("int", typeof(int))]
    [InlineData("bigint", typeof(long))]
    [InlineData("decimal(18,2)", typeof(decimal))]
    [InlineData("float", typeof(double))]
    [InlineData("datetime2", typeof(DateTime))]
    [InlineData("uuid", typeof(Guid))]
    [InlineData("varchar(10)", typeof(string))]
    [InlineData("something-unknown", typeof(string))]
    public void GetTypeObject_ReturnsTheMappedDotNetType(string sqlType, Type expected) =>
        Assert.Equal(expected, SqlTypeMapper.GetTypeObject(sqlType));

    // ── GetDBType ────────────────────────────────────────────────────────────

    // datetime2 maps to DbType.DateTime, never to DbType.DateTime2.
    [Theory]
    [InlineData("bit", DbType.Boolean)]
    [InlineData("boolean", DbType.Boolean)]
    [InlineData("tinyint", DbType.UInt16)]
    [InlineData("smallint", DbType.Int16)]
    [InlineData("int", DbType.Int32)]
    [InlineData("integer", DbType.Int32)]
    [InlineData("bigint", DbType.Int64)]
    [InlineData("decimal(18,2)", DbType.Decimal)]
    [InlineData("money", DbType.Decimal)]
    [InlineData("float", DbType.Double)]
    [InlineData("real", DbType.Double)]
    [InlineData("date", DbType.DateTime)]
    [InlineData("datetime", DbType.DateTime)]
    [InlineData("datetime2", DbType.DateTime)]
    [InlineData("timestamp", DbType.DateTime)]
    [InlineData("uniqueidentifier", DbType.Guid)]
    [InlineData("uuid", DbType.Guid)]
    [InlineData("varchar(50)", DbType.String)]
    [InlineData("nvarchar(max)", DbType.String)]
    [InlineData("text", DbType.String)]
    [InlineData("unknown_type", DbType.String)]
    [InlineData("", DbType.String)]
    public void GetDBType_SqlTypeName_ReturnsMatchingDbType(string sqlType, DbType expected) =>
        Assert.Equal(expected, SqlTypeMapper.GetDBType(sqlType));

    // The input is a SQL type name, not a .NET type name. A .NET name is recognized only when it
    // happens to coincide with a SQL alias, so passing "Int64" silently yields DbType.String.
    [Theory]
    [InlineData("Int16")]
    [InlineData("Int32")]
    [InlineData("Int64")]
    [InlineData("Guid")]
    [InlineData("Byte")]
    [InlineData("System.Int64")]
    public void GetDBType_DotNetTypeName_IsNotASqlAliasAndFallsBackToString(string typeName) =>
        Assert.Equal(DbType.String, SqlTypeMapper.GetDBType(typeName));

    [Theory]
    [InlineData("Boolean", DbType.Boolean)]
    [InlineData("Decimal", DbType.Decimal)]
    [InlineData("Double", DbType.Double)]
    [InlineData("DateTime", DbType.DateTime)]
    public void GetDBType_DotNetTypeName_MatchesOnlyWhenItIsAlsoASqlAlias(
        string typeName,
        DbType expected
    ) => Assert.Equal(expected, SqlTypeMapper.GetDBType(typeName));

    [Fact]
    public void GetDBType_NullTypeName_FallsBackToString() =>
        Assert.Equal(DbType.String, SqlTypeMapper.GetDBType(null));

    // ── IsCharTypeDefinition ─────────────────────────────────────────────────

    [Theory]
    [InlineData("varchar(50)")]
    [InlineData("VARCHAR(50)")]
    [InlineData("nvarchar(255)")]
    [InlineData("char(10)")]
    [InlineData("NCHAR(10)")]
    [InlineData("varchar()")]
    [InlineData(" char(3) ")]
    [InlineData("varchar(10) NOT NULL")]
    public void IsCharTypeDefinition_CharTypeWithLength_ReturnsTrue(string definition) =>
        Assert.True(SqlTypeMapper.IsCharTypeDefinition(definition));

    // varchar(max) does not count: the length must be digits.
    [Theory]
    [InlineData("varchar")]
    [InlineData("varchar(max)")]
    [InlineData("nvarchar(MAX)")]
    [InlineData("varchar (50)")]
    [InlineData("character varying(10)")]
    [InlineData("text")]
    [InlineData("ntext")]
    [InlineData("int")]
    [InlineData("string")]
    [InlineData("")]
    public void IsCharTypeDefinition_AnythingElse_ReturnsFalse(string definition) =>
        Assert.False(SqlTypeMapper.IsCharTypeDefinition(definition));

    // ── GetStringLengthFromCharString ────────────────────────────────────────

    [Theory]
    [InlineData("varchar(50)", 50)]
    [InlineData("VARCHAR(50)", 50)]
    [InlineData("nvarchar(255)", 255)]
    [InlineData("char(1)", 1)]
    [InlineData("nchar(0)", 0)]
    [InlineData("varchar(300)", 300)]
    [InlineData("varchar(2147483647)", int.MaxValue)]
    [InlineData("  nvarchar(40)", 40)]
    [InlineData("varchar(10) ", 10)]
    public void GetStringLengthFromCharString_CharTypeWithLength_ReturnsTheLength(
        string definition,
        int expected
    ) => Assert.Equal(expected, SqlTypeMapper.GetStringLengthFromCharString(definition));

    [Theory]
    [InlineData("varchar")]
    [InlineData("varchar(max)")]
    [InlineData("varchar()")]
    [InlineData("varchar(99999999999)")]
    [InlineData("text")]
    [InlineData("")]
    public void GetStringLengthFromCharString_NoNumericLength_ReturnsDefaultStringLength(
        string definition
    ) =>
        Assert.Equal(
            SqlTypeMapper.DefaultStringLength,
            SqlTypeMapper.GetStringLengthFromCharString(definition)
        );

    // Text before the char type is swallowed by the match, text after it is kept and breaks the
    // integer parse, so the default length wins even though a length is present.
    [Fact]
    public void GetStringLengthFromCharString_TrailingText_FallsBackToDefaultStringLength() =>
        Assert.Equal(
            SqlTypeMapper.DefaultStringLength,
            SqlTypeMapper.GetStringLengthFromCharString("varchar(10) NOT NULL")
        );

    // ── GetNETDateTimeKind ───────────────────────────────────────────────────

    [Theory]
    [InlineData("date")]
    [InlineData("datetime")]
    [InlineData("smalldatetime")]
    [InlineData("datetime2")]
    [InlineData("time")]
    [InlineData("timestamp")]
    public void GetNETDateTimeKind_DateTimeType_ReturnsUnspecified(string sqlType) =>
        Assert.Equal(DateTimeKind.Unspecified, SqlTypeMapper.GetNETDateTimeKind(sqlType));

    [Theory]
    [InlineData("timetz")]
    [InlineData("timestamptz")]
    public void GetNETDateTimeKind_TimeZoneAwareType_ReturnsUtc(string sqlType) =>
        Assert.Equal(DateTimeKind.Utc, SqlTypeMapper.GetNETDateTimeKind(sqlType));

    [Theory]
    [InlineData("int")]
    [InlineData("varchar(10)")]
    [InlineData("datetimeoffset")]
    [InlineData("")]
    public void GetNETDateTimeKind_NonDateTimeType_ReturnsNull(string sqlType) =>
        Assert.Null(SqlTypeMapper.GetNETDateTimeKind(sqlType));

    [Fact]
    public void GetNETDateTimeKind_NullTypeName_ReturnsNull() =>
        Assert.Null(SqlTypeMapper.GetNETDateTimeKind(null));

    // Unlike GetNETObjectTypeString, this lookup is case-sensitive and does not strip a length
    // suffix, so only the exact lower-case name matches.
    [Theory]
    [InlineData("DATETIME")]
    [InlineData("DateTime2")]
    [InlineData("TIMESTAMPTZ")]
    [InlineData(" datetime")]
    [InlineData("datetime2(7)")]
    public void GetNETDateTimeKind_IsCaseSensitiveAndExact(string sqlType) =>
        Assert.Null(SqlTypeMapper.GetNETDateTimeKind(sqlType));
}
