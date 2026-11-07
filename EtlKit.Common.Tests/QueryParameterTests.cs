#nullable enable
using System.Data;
using Xunit;

namespace EtlKit.Common.Tests;

/// <summary>
/// Tests for <see cref="QueryParameter"/>, which derives its <see cref="QueryParameter.DBType"/>
/// from a SQL type name via <see cref="SqlTypeMapper.GetDBType"/> unless it was given an explicit
/// <see cref="DbType"/>. The type lives in EtlKit.Common but keeps its full name and is forwarded
/// from the main EtlKit assembly (see docs/tech-debt/TECH-DEBT-DataTypeConverter-Driver-Split.md).
/// </summary>
public sealed class QueryParameterTests
{
    // The type keeps its name and namespace when it moves, so existing callers still compile.
    [Fact]
    public void FullName_IsStable() =>
        Assert.Equal("EtlKit.QueryParameter", typeof(QueryParameter).FullName);

    // The main assembly forwards the type, so assemblies compiled against an older EtlKit that
    // defined QueryParameter itself keep resolving it.
    [Fact]
    public void LivesInCommon_AndIsForwardedFromTheMainAssembly()
    {
        var mainAssembly = typeof(TableColumn).Assembly;

        Assert.Equal("EtlKit.Common", typeof(QueryParameter).Assembly.GetName().Name);
        Assert.Contains(typeof(QueryParameter), mainAssembly.GetForwardedTypes());
        Assert.Same(typeof(QueryParameter), mainAssembly.GetType("EtlKit.QueryParameter"));
    }

    // ── constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_StoresNameTypeAndValue()
    {
        var parameter = new QueryParameter("id", "bigint", 42L);

        Assert.Equal("id", parameter.Name);
        Assert.Equal("bigint", parameter.Type);
        Assert.Equal(42L, parameter.Value);
    }

    [Fact]
    public void Constructor_NullValue_IsStoredAsDBNull() =>
        Assert.Same(DBNull.Value, new QueryParameter("p", "int", null).Value);

    [Fact]
    public void Constructor_NonNullValue_IsStoredUnchanged()
    {
        var value = new object();

        Assert.Same(value, new QueryParameter("p", "varchar(10)", value).Value);
    }

    // ── DBType ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("int", DbType.Int32)]
    [InlineData("bigint", DbType.Int64)]
    [InlineData("bit", DbType.Boolean)]
    [InlineData("decimal(18,2)", DbType.Decimal)]
    [InlineData("datetime2", DbType.DateTime)]
    [InlineData("uniqueidentifier", DbType.Guid)]
    [InlineData("varchar(10)", DbType.String)]
    [InlineData("something-unknown", DbType.String)]
    public void DBType_IsDerivedFromTheSqlTypeName(string type, DbType expected) =>
        Assert.Equal(expected, new QueryParameter("p", type, null).DBType);

    [Fact]
    public void DBType_NullTypeName_FallsBackToString() =>
        Assert.Equal(DbType.String, new QueryParameter("p", null, 1).DBType);

    // The type is a SQL type name, not a .NET type name: "Int64" is not a SQL alias, so the
    // parameter silently becomes a string. A caller that knows the DbType up front passes it to
    // the constructor that takes a DbType instead.
    [Theory]
    [InlineData("Int16")]
    [InlineData("Int32")]
    [InlineData("Int64")]
    [InlineData("Guid")]
    [InlineData("System.Int64")]
    public void DBType_DotNetTypeName_FallsBackToString(string type) =>
        Assert.Equal(DbType.String, new QueryParameter("p", type, 1).DBType);

    // ── explicit DbType ──────────────────────────────────────────────────────

    [Fact]
    public void ConstructorWithDbType_StoresNameAndValue()
    {
        var parameter = new QueryParameter("id", DbType.Int64, 42L);

        Assert.Equal("id", parameter.Name);
        Assert.Equal(42L, parameter.Value);
    }

    // DateTime2 and Object cannot be reached through a SQL type name at all.
    [Theory]
    [InlineData(DbType.Int64)]
    [InlineData(DbType.String)]
    [InlineData(DbType.Guid)]
    [InlineData(DbType.DateTime2)]
    [InlineData(DbType.DateTimeOffset)]
    [InlineData(DbType.Object)]
    public void ConstructorWithDbType_ExposesTheGivenDbType(DbType dbType) =>
        Assert.Equal(dbType, new QueryParameter("p", dbType, 1).DBType);

    [Fact]
    public void ConstructorWithDbType_SetsTypeToTheDbTypeName() =>
        Assert.Equal("Int64", new QueryParameter("p", DbType.Int64, 1L).Type);

    [Fact]
    public void ConstructorWithDbType_NullValue_IsStoredAsDBNull() =>
        Assert.Same(DBNull.Value, new QueryParameter("p", DbType.String, null).Value);
}
