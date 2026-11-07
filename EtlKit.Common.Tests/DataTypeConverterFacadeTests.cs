#nullable enable
using System.Data;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using Xunit;

namespace EtlKit.Common.Tests;

/// <summary>
/// One check per member of the obsolete <see cref="DataTypeConverter"/>: each must answer as its
/// replacement does. The behavior itself is pinned where the replacement is tested, in <see
/// cref="SqlTypeMapperTests"/> and <see cref="ConnectionManagerTypeConventionsTests"/>. Delete this
/// class together with the members once they are removed.
/// </summary>
#pragma warning disable CS0618
public sealed class DataTypeConverterFacadeTests
{
    [Fact]
    public void FullName_IsStable() =>
        Assert.Equal(
            "EtlKit.ConnectionManager.DataTypeConverter",
            typeof(DataTypeConverter).FullName
        );

    [Fact]
    public void DefaultLengths_ForwardToSqlTypeMapper()
    {
        Assert.Equal(
            SqlTypeMapper.DefaultTinyIntegerLength,
            DataTypeConverter.DefaultTinyIntegerLength
        );
        Assert.Equal(
            SqlTypeMapper.DefaultSmallIntegerLength,
            DataTypeConverter.DefaultSmallIntegerLength
        );
        Assert.Equal(SqlTypeMapper.DefaultIntegerLength, DataTypeConverter.DefaultIntegerLength);
        Assert.Equal(
            SqlTypeMapper.DefaultBigIntegerLength,
            DataTypeConverter.DefaultBigIntegerLength
        );
        Assert.Equal(
            SqlTypeMapper.DefaultDateTime2Length,
            DataTypeConverter.DefaultDateTime2Length
        );
        Assert.Equal(SqlTypeMapper.DefaultDateTimeLength, DataTypeConverter.DefaultDateTimeLength);
        Assert.Equal(SqlTypeMapper.DefaultDecimalLength, DataTypeConverter.DefaultDecimalLength);
        Assert.Equal(SqlTypeMapper.DefaultStringLength, DataTypeConverter.DefaultStringLength);
    }

    [Fact]
    public void IsCharTypeDefinition_ForwardsToSqlTypeMapper()
    {
        Assert.True(DataTypeConverter.IsCharTypeDefinition("varchar(50)"));
        Assert.False(DataTypeConverter.IsCharTypeDefinition("varchar(max)"));
    }

    [Fact]
    public void GetStringLengthFromCharString_ForwardsToSqlTypeMapper() =>
        Assert.Equal(50, DataTypeConverter.GetStringLengthFromCharString("varchar(50)"));

    [Fact]
    public void GetNETObjectTypeString_ForwardsToSqlTypeMapper() =>
        Assert.Equal("System.Int64", DataTypeConverter.GetNETObjectTypeString("bigint"));

    [Fact]
    public void GetTypeObject_ForwardsToSqlTypeMapper() =>
        Assert.Equal(typeof(long), DataTypeConverter.GetTypeObject("bigint"));

    [Fact]
    public void GetDBType_ForwardsToSqlTypeMapper() =>
        Assert.Equal(DbType.Int64, DataTypeConverter.GetDBType("bigint"));

    [Fact]
    public void GetNETDateTimeKind_ForwardsToSqlTypeMapper() =>
        Assert.Equal(DateTimeKind.Utc, DataTypeConverter.GetNETDateTimeKind("timestamptz"));

    // Routes by engine: an engine with its own rule, one without, and a value that is no engine.
    [Theory]
    [InlineData(ConnectionManagerType.SqlServer, "TEXT", false, "VARCHAR(MAX)")]
    [InlineData(ConnectionManagerType.Access, "INT", false, "INTEGER")]
    [InlineData(ConnectionManagerType.SQLite, "BIGINT", false, "INTEGER")]
    [InlineData(ConnectionManagerType.Postgres, "DATETIME", false, "TIMESTAMP")]
    [InlineData(ConnectionManagerType.ClickHouse, "INT", true, "Nullable(INT)")]
    [InlineData(ConnectionManagerType.MySql, "TEXT", false, "TEXT")]
    [InlineData(ConnectionManagerType.Adomd, "TEXT", false, "TEXT")]
    [InlineData(ConnectionManagerType.Unknown, "  nvarchar(300) ", true, "  nvarchar(300) ")]
    [InlineData((ConnectionManagerType)999, "  nvarchar(300) ", true, "  nvarchar(300) ")]
    public void TryGetDBSpecificType_RoutesByEngine(
        ConnectionManagerType engine,
        string dataType,
        bool allowNulls,
        string expected
    ) =>
        Assert.Equal(
            expected,
            DataTypeConverter.TryGetDBSpecificType(
                new TableColumn("col", dataType, allowNulls),
                engine
            )
        );
}
#pragma warning restore CS0618
