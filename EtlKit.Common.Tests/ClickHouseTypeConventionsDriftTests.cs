#nullable enable
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using Xunit;

namespace EtlKit.Common.Tests;

/// <summary>
/// The ClickHouse rule exists twice: in <c>EtlKit.ClickHouse</c>, where <c>ClickHouseConnectionManager</c>
/// supplies it, and as a copy in the main package for the managers that get conventions by the engine
/// they report (a custom manager that reports ClickHouse), because the ClickHouse package depends on the
/// main one. This keeps the two from drifting apart. Delete it together with the copy.
/// </summary>
public sealed class ClickHouseTypeConventionsDriftTests
{
    private static readonly string[] DataTypes =
    [
        "TEXT",
        "text",
        "INT",
        "int",
        "  int  ",
        "BIGINT",
        "decimal(18,2)",
        "VARCHAR(10)",
        "varchar(50)",
        "varchar(256)",
        "nvarchar(50)",
        "NVARCHAR(300)",
        "nchar(10)",
        "varchar(max)",
        "DATETIME",
        "datetime2",
        "uniqueidentifier",
        "Int32",
        "String",
        "Nullable(Int32)",
        "Nullable(String)",
    ];

    [Fact]
    public void TheCopyForCustomManagers_MatchesTheRuleInTheClickHousePackage()
    {
        using var clickHouse = TestConnectionManagers.Get("ClickHouse");
        using var custom = new CustomDbConnectionManager(ConnectionManagerType.ClickHouse);

        var mismatches = new List<string>();
        foreach (var dataType in DataTypes)
        {
            foreach (var allowNulls in new[] { false, true })
            {
                var column = new TableColumn("col", dataType, allowNulls);
                var expected = clickHouse.Conventions.GetDbSpecificType(column);
                var actual = custom.TypeConventions.GetDbSpecificType(column);
                if (actual != expected)
                    mismatches.Add(
                        $"'{dataType}' allowNulls={allowNulls}: package '{expected}', copy '{actual}'"
                    );
            }
        }

        Assert.Empty(mismatches);
    }
}
