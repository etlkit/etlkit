#nullable enable
using EtlKit.ClickHouse.ConnectionManager;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using Xunit;

namespace EtlKit.Common.Tests;

/// <summary>
/// Characterization tests for the SQL type that the <c>TypeConventions</c> of each real connection
/// manager create a column with, which is where <c>CreateTableTask</c> takes it from. They pin today's
/// rules, quirks included (upper-casing, untrimmed pass-through, <c>varchar(max)</c> not counting as a
/// char type), so the rules can move between assemblies without changing what <c>CreateTableTask</c>
/// emits. A quirk pinned here is not an endorsement: fixing one is a separate change that updates its
/// expectation on purpose.
/// </summary>
public sealed class ConnectionManagerTypeConventionsTests
{
    // The manager names in TestConnectionManagers match the ConnectionManagerType names.
    private static string MapType(ConnectionManagerType engine, ITableColumn column)
    {
        using var managerCase = TestConnectionManagers.Get(engine.ToString());

        return managerCase.Conventions.GetDbSpecificType(column);
    }

    private static string MapType(
        ConnectionManagerType engine,
        string dataType,
        bool allowNulls = false
    ) => MapType(engine, new TableColumn("col", dataType, allowNulls));

    // Every engine that has a connection manager. Unknown has none and is covered by the tests of the
    // obsolete engine-keyed facade and of custom managers.
    private static readonly ConnectionManagerType[] Engines =
    [
        ConnectionManagerType.Adomd,
        ConnectionManagerType.MySql,
        ConnectionManagerType.SqlServer,
        ConnectionManagerType.SQLite,
        ConnectionManagerType.Access,
        ConnectionManagerType.Postgres,
        ConnectionManagerType.ClickHouse,
    ];

    // ── matrix: every engine x typical column types x nullability ────────────

    private sealed record EngineRow(
        ConnectionManagerType Engine,
        string[] Expected,
        string[]? ExpectedWhenNullable = null
    );

    // Columns of every row below, in this order. ExpectedWhenNullable is only given for engines
    // whose result depends on AllowNulls; for the others both nullabilities share Expected.
    // Engines without conventions expect every type back unchanged, i.e. MatrixTypes itself.
    private static readonly string[] MatrixTypes =
    [
        "TEXT",
        "INT",
        "BIGINT",
        "VARCHAR(10)",
        "NVARCHAR(300)",
        "DATETIME",
    ];

    private static readonly EngineRow[] Matrix =
    [
        new(ConnectionManagerType.Adomd, MatrixTypes),
        new(ConnectionManagerType.MySql, MatrixTypes),
        new(
            ConnectionManagerType.SqlServer,
            ["VARCHAR(MAX)", "INT", "BIGINT", "VARCHAR(10)", "NVARCHAR(300)", "DATETIME"]
        ),
        new(
            ConnectionManagerType.SQLite,
            ["TEXT", "INTEGER", "INTEGER", "VARCHAR(10)", "NVARCHAR(300)", "DATETIME"]
        ),
        new(
            ConnectionManagerType.Access,
            ["TEXT", "INTEGER", "BIGINT", "VARCHAR(10)", "LONGTEXT", "DATETIME"]
        ),
        new(
            ConnectionManagerType.Postgres,
            ["TEXT", "INT", "BIGINT", "VARCHAR(10)", "VARCHAR(300)", "TIMESTAMP"]
        ),
        new(
            ConnectionManagerType.ClickHouse,
            ["TEXT", "INT", "BIGINT", "String", "String", "DATETIME"],
            [
                "Nullable(TEXT)",
                "Nullable(INT)",
                "Nullable(BIGINT)",
                "Nullable(String)",
                "Nullable(String)",
                "Nullable(DATETIME)",
            ]
        ),
    ];

    public static TheoryData<ConnectionManagerType, string, bool, string> MatrixCases
    {
        get
        {
            var data = new TheoryData<ConnectionManagerType, string, bool, string>();
            foreach (var row in Matrix)
            {
                for (var i = 0; i < MatrixTypes.Length; i++)
                {
                    data.Add(row.Engine, MatrixTypes[i], false, row.Expected[i]);
                    data.Add(
                        row.Engine,
                        MatrixTypes[i],
                        true,
                        (row.ExpectedWhenNullable ?? row.Expected)[i]
                    );
                }
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(MatrixCases))]
    public void Matrix_ReturnsEngineSpecificType(
        ConnectionManagerType engine,
        string dataType,
        bool allowNulls,
        string expected
    ) => Assert.Equal(expected, MapType(engine, dataType, allowNulls));

    // Adding an engine must be a conscious decision: it has to get a row in the matrix above.
    [Fact]
    public void Matrix_CoversEveryEngineExceptUnknown()
    {
        var covered = Matrix.Select(row => row.Engine).ToHashSet();

        var missing = Enum.GetValues<ConnectionManagerType>()
            .Where(engine => engine != ConnectionManagerType.Unknown && !covered.Contains(engine))
            .ToList();

        Assert.Empty(missing);
        Assert.Equal(Engines.OrderBy(e => e), covered.OrderBy(e => e));
    }

    // ── every concrete manager ───────────────────────────────────────────────

    // What each listed manager must make of one probe column. SQL Server over ODBC is a manager of its
    // own next to the native one and must apply the same rules.
    private static readonly (
        string Name,
        string DataType,
        bool AllowNulls,
        string Expected
    )[] ManagerRules =
    [
        ("SqlServer", "TEXT", false, "VARCHAR(MAX)"),
        ("SqlServerOdbc", "TEXT", false, "VARCHAR(MAX)"),
        ("Postgres", "NVARCHAR(300)", false, "VARCHAR(300)"),
        ("MySql", "NVARCHAR(300)", false, "NVARCHAR(300)"),
        ("SQLite", "BIGINT", false, "INTEGER"),
        ("Access", "NVARCHAR(300)", false, "LONGTEXT"),
        ("ClickHouse", "VARCHAR(10)", true, "Nullable(String)"),
        ("Adomd", "TEXT", false, "TEXT"),
    ];

    public static TheoryData<string, string, bool, string> ManagerRuleCases
    {
        get
        {
            var data = new TheoryData<string, string, bool, string>();
            foreach (var rule in ManagerRules)
                data.Add(rule.Name, rule.DataType, rule.AllowNulls, rule.Expected);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ManagerRuleCases))]
    public void Manager_AppliesItsEngineRules(
        string name,
        string dataType,
        bool allowNulls,
        string expected
    )
    {
        using var managerCase = TestConnectionManagers.Get(name);

        Assert.Equal(
            expected,
            managerCase.Conventions.GetDbSpecificType(new TableColumn("col", dataType, allowNulls))
        );
    }

    [Fact]
    public void ManagerRules_CoverEveryListedManager() =>
        Assert.Equal(
            TestConnectionManagers.Names.OrderBy(name => name),
            ManagerRules.Select(rule => rule.Name).OrderBy(name => name)
        );

    // A new connection manager, even one for an engine that is already listed, has to be added to
    // TestConnectionManagers.Names and ManagerRules: that is the moment to decide which rules it applies.
    [Fact]
    public void EveryConcreteConnectionManager_IsListed()
    {
        var concrete = new[]
        {
            typeof(SqlConnectionManager).Assembly,
            typeof(ClickHouseConnectionManager).Assembly,
        }
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => !type.IsAbstract && DerivesFromDbConnectionManager(type))
            .ToHashSet();

        var listed = TestConnectionManagers
            .Names.Select(name =>
            {
                using var managerCase = TestConnectionManagers.Get(name);
                return managerCase.Manager.GetType();
            })
            .ToHashSet();

        Assert.Empty(concrete.Except(listed).Select(type => type.FullName));
    }

    private static bool DerivesFromDbConnectionManager(Type type)
    {
        for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (
                baseType.IsGenericType
                && baseType.GetGenericTypeDefinition() == typeof(DbConnectionManager<>)
            )
                return true;
        }
        return false;
    }

    // ── engines without conventions ──────────────────────────────────────────

    public static TheoryData<ConnectionManagerType, string, bool> UnchangedCases
    {
        get
        {
            var data = new TheoryData<ConnectionManagerType, string, bool>();
            foreach (
                var engine in new[] { ConnectionManagerType.Adomd, ConnectionManagerType.MySql }
            )
            {
                foreach (
                    var dataType in new[]
                    {
                        "text",
                        "  int  ",
                        "nvarchar(300)",
                        "Nullable(Int32)",
                        "datetime",
                    }
                )
                {
                    data.Add(engine, dataType, false);
                    data.Add(engine, dataType, true);
                }
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(UnchangedCases))]
    public void EngineWithoutConventions_ReturnsDataTypeVerbatim(
        ConnectionManagerType engine,
        string dataType,
        bool allowNulls
    ) => Assert.Equal(dataType, MapType(engine, dataType, allowNulls));

    [Fact]
    public void MapType_DoesNotModifyTheColumn()
    {
        var column = new TableColumn("col", "  nvarchar(300) ", allowNulls: true);

        foreach (var engine in Engines)
            MapType(engine, column);

        Assert.Equal("  nvarchar(300) ", column.DataType);
        Assert.True(column.AllowNulls);
    }

    // ── SQL Server ───────────────────────────────────────────────────────────

    // Only TEXT is rewritten; the comparison ignores case, surrounding whitespace and inner
    // spaces. Everything else is returned verbatim, including its original casing and spaces.
    [Theory]
    [InlineData("TEXT", "VARCHAR(MAX)")]
    [InlineData("text", "VARCHAR(MAX)")]
    [InlineData("  Text  ", "VARCHAR(MAX)")]
    [InlineData("te xt", "VARCHAR(MAX)")]
    [InlineData("ntext", "ntext")]
    [InlineData("varchar(max)", "varchar(max)")]
    [InlineData("int", "int")]
    [InlineData("  int  ", "  int  ")]
    [InlineData("nvarchar(300)", "nvarchar(300)")]
    [InlineData("datetime", "datetime")]
    public void SqlServer_RewritesOnlyText(string dataType, string expected) =>
        Assert.Equal(expected, MapType(ConnectionManagerType.SqlServer, dataType));

    // ── Access ───────────────────────────────────────────────────────────────

    // INT becomes INTEGER; char types lose a leading N and come back upper-cased, or become
    // LONGTEXT above 255 characters. Everything else is returned verbatim.
    [Theory]
    [InlineData("INT", "INTEGER")]
    [InlineData("int", "INTEGER")]
    [InlineData("  Int ", "INTEGER")]
    [InlineData("varchar(50)", "VARCHAR(50)")]
    [InlineData("nvarchar(50)", "VARCHAR(50)")]
    [InlineData("NCHAR(10)", "CHAR(10)")]
    [InlineData("char(10)", "CHAR(10)")]
    [InlineData("  varchar(50)  ", "VARCHAR(50)")]
    [InlineData("varchar(255)", "VARCHAR(255)")]
    [InlineData("nvarchar(255)", "VARCHAR(255)")]
    [InlineData("varchar(256)", "LONGTEXT")]
    [InlineData("nvarchar(300)", "LONGTEXT")]
    [InlineData("char(1000)", "LONGTEXT")]
    [InlineData("bigint", "bigint")]
    [InlineData("integer", "integer")]
    [InlineData("text", "text")]
    [InlineData("datetime", "datetime")]
    [InlineData("varchar(max)", "varchar(max)")]
    [InlineData("  bigint  ", "  bigint  ")]
    public void Access_AdaptsIntAndCharTypes(string dataType, string expected) =>
        Assert.Equal(expected, MapType(ConnectionManagerType.Access, dataType));

    // ── SQLite ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("int", "INTEGER")]
    [InlineData("INT", "INTEGER")]
    [InlineData("  int  ", "INTEGER")]
    [InlineData("bigint", "INTEGER")]
    [InlineData("BigInt", "INTEGER")]
    [InlineData("integer", "integer")]
    [InlineData("smallint", "smallint")]
    [InlineData("tinyint", "tinyint")]
    [InlineData("  tinyint  ", "  tinyint  ")]
    [InlineData("varchar(50)", "varchar(50)")]
    [InlineData("text", "text")]
    [InlineData("datetime", "datetime")]
    public void SQLite_MapsIntAndBigIntToInteger(string dataType, string expected) =>
        Assert.Equal(expected, MapType(ConnectionManagerType.SQLite, dataType));

    // ── PostgreSQL ───────────────────────────────────────────────────────────

    // A char type starting with N loses it and comes back upper-cased; a char type without a
    // leading N is returned verbatim (original casing). DATETIME becomes TIMESTAMP, but datetime2
    // is left alone even though PostgreSQL has no such type.
    [Theory]
    [InlineData("nvarchar(50)", "VARCHAR(50)")]
    [InlineData("NVARCHAR(50)", "VARCHAR(50)")]
    [InlineData("nchar(10)", "CHAR(10)")]
    [InlineData("varchar(50)", "varchar(50)")]
    [InlineData("VARCHAR(50)", "VARCHAR(50)")]
    [InlineData("char(5)", "char(5)")]
    [InlineData("datetime", "TIMESTAMP")]
    [InlineData("DATETIME", "TIMESTAMP")]
    [InlineData("  DateTime  ", "TIMESTAMP")]
    [InlineData("datetime2", "datetime2")]
    [InlineData("timestamp", "timestamp")]
    [InlineData("int", "int")]
    [InlineData("text", "text")]
    [InlineData("bit", "bit")]
    [InlineData("nvarchar(max)", "nvarchar(max)")]
    [InlineData("ntext", "ntext")]
    public void Postgres_AdaptsCharTypesAndDateTime(string dataType, string expected) =>
        Assert.Equal(expected, MapType(ConnectionManagerType.Postgres, dataType));

    // ── ClickHouse ───────────────────────────────────────────────────────────

    // Char types become String; a nullable column is wrapped in Nullable(...) unless its type
    // already starts with "Nullable". The wrap is the only place AllowNulls matters.
    [Theory]
    [InlineData("Int32", false, "Int32")]
    [InlineData("Int32", true, "Nullable(Int32)")]
    [InlineData("String", false, "String")]
    [InlineData("String", true, "Nullable(String)")]
    [InlineData("varchar(50)", false, "String")]
    [InlineData("varchar(50)", true, "Nullable(String)")]
    [InlineData("NVARCHAR(100)", false, "String")]
    [InlineData("nchar(10)", true, "Nullable(String)")]
    [InlineData("Nullable(Int32)", false, "Nullable(Int32)")]
    [InlineData("Nullable(Int32)", true, "Nullable(Int32)")]
    [InlineData("Nullable(String)", true, "Nullable(String)")]
    [InlineData("varchar(max)", false, "varchar(max)")]
    [InlineData("varchar(max)", true, "Nullable(varchar(max))")]
    [InlineData("text", false, "text")]
    [InlineData("datetime", true, "Nullable(datetime)")]
    [InlineData("  Int32  ", false, "  Int32  ")]
    public void ClickHouse_AdaptsCharTypesAndWrapsNullable(
        string dataType,
        bool allowNulls,
        string expected
    ) => Assert.Equal(expected, MapType(ConnectionManagerType.ClickHouse, dataType, allowNulls));
}
