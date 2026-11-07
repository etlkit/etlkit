#nullable enable
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using Xunit;

namespace EtlKit.Common.Tests;

/// <summary>
/// A user's own subclass of <see cref="DbConnectionManager{TConnection}"/> or <see
/// cref="OdbcConnectionManager"/> reports its engine itself. Before type conventions existed,
/// <c>CreateTableTask</c> picked the rules by that engine, so such a manager must keep getting
/// them, unless it supplies conventions of its own.
/// </summary>
public sealed class CustomConnectionManagerTypeConventionsTests
{
    public static TheoryData<ConnectionManagerType, string, bool, string> EngineRules =>
        new()
        {
            { ConnectionManagerType.SqlServer, "TEXT", false, "VARCHAR(MAX)" },
            { ConnectionManagerType.Postgres, "NVARCHAR(300)", false, "VARCHAR(300)" },
            { ConnectionManagerType.Postgres, "DATETIME", false, "TIMESTAMP" },
            { ConnectionManagerType.Access, "NVARCHAR(300)", false, "LONGTEXT" },
            { ConnectionManagerType.SQLite, "BIGINT", false, "INTEGER" },
            { ConnectionManagerType.ClickHouse, "VARCHAR(10)", true, "Nullable(String)" },
            { ConnectionManagerType.MySql, "NVARCHAR(300)", false, "NVARCHAR(300)" },
            { ConnectionManagerType.Adomd, "TEXT", false, "TEXT" },
            { ConnectionManagerType.Unknown, "DATETIME", true, "DATETIME" },
        };

    private static string Adapt(IDbTypeConventions conventions, string dataType, bool allowNulls) =>
        conventions.GetDbSpecificType(new TableColumn("col", dataType, allowNulls));

    [Theory]
    [MemberData(nameof(EngineRules))]
    public void OdbcSubclass_GetsTheRulesOfTheEngineItReports(
        ConnectionManagerType engine,
        string dataType,
        bool allowNulls,
        string expected
    )
    {
        using var manager = new CustomOdbcConnectionManager(engine);

        Assert.Equal(expected, Adapt(manager.TypeConventions, dataType, allowNulls));
    }

    [Theory]
    [MemberData(nameof(EngineRules))]
    public void DbConnectionManagerSubclass_GetsTheRulesOfTheEngineItReports(
        ConnectionManagerType engine,
        string dataType,
        bool allowNulls,
        string expected
    )
    {
        using var manager = new CustomDbConnectionManager(engine);

        Assert.Equal(expected, Adapt(manager.TypeConventions, dataType, allowNulls));
    }

    [Fact]
    public void Subclass_CanSupplyItsOwnConventions()
    {
        var own = new EveryColumnIsTextConventions();
        using var manager = new CustomManagerWithOwnConventions(own);

        Assert.Same(own, manager.TypeConventions);
    }
}
