#nullable enable
using Xunit;

namespace EtlKit.Common.Tests;

/// <summary>
/// Characterization tests for the two <see cref="TableColumn"/> properties that delegate to the
/// type mapping in <see cref="SqlTypeMapper"/>: <see cref="TableColumn.NETDataType"/> and <see
/// cref="TableColumn.NETDateTimeKind"/>. They guard the call sites that were rewired when the
/// mapping moved (see docs/tech-debt/TECH-DEBT-DataTypeConverter-Driver-Split.md).
/// </summary>
public sealed class TableColumnTypeMappingTests
{
    [Theory]
    [InlineData("int", typeof(int))]
    [InlineData("INT", typeof(int))]
    [InlineData("bigint", typeof(long))]
    [InlineData("nvarchar(50)", typeof(string))]
    [InlineData("decimal(18,2)", typeof(decimal))]
    [InlineData("DATETIME", typeof(DateTime))]
    [InlineData("uniqueidentifier", typeof(Guid))]
    public void NETDataType_IsResolvedFromTheSqlDataType(string dataType, Type expected) =>
        Assert.Equal(expected, new TableColumn("c", dataType).NETDataType);

    [Theory]
    [InlineData("date", DateTimeKind.Unspecified)]
    [InlineData("datetime", DateTimeKind.Unspecified)]
    [InlineData("datetime2", DateTimeKind.Unspecified)]
    [InlineData("timestamptz", DateTimeKind.Utc)]
    public void NETDateTimeKind_DateTimeColumn_IsResolvedFromTheSqlDataType(
        string dataType,
        DateTimeKind expected
    ) => Assert.Equal(expected, new TableColumn("c", dataType).NETDateTimeKind);

    // The lookup is case-sensitive: an upper-case date/time type name yields no kind at all,
    // although NETDataType resolves the same name to DateTime.
    [Theory]
    [InlineData("int")]
    [InlineData("varchar(10)")]
    [InlineData("DATETIME")]
    public void NETDateTimeKind_NonLowerCaseDateTimeColumn_IsNull(string dataType) =>
        Assert.Null(new TableColumn("c", dataType).NETDateTimeKind);
}
