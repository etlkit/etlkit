#nullable enable
using System.Globalization;
using EtlKit.ConnectionManager;
using EtlKit.Primitives;
using Microsoft.Data.Sqlite;

namespace EtlKit.Common.Tests;

/// <summary>
/// A user's own <see cref="OdbcConnectionManager"/> subclass, for example PostgreSQL over ODBC. It
/// reports its engine itself and supplies no type conventions of its own unless a subclass does.
/// </summary>
internal class CustomOdbcConnectionManager(ConnectionManagerType engine) : OdbcConnectionManager
{
    public override ConnectionManagerType ConnectionManagerType => engine;

    public override string QB => "\"";

    public override string QE => "\"";

    public override CultureInfo ConnectionCulture => CultureInfo.InvariantCulture;

    public override void BulkInsert(ITableData data, string tableName) { }

    public override void PrepareBulkInsert(string tableName) { }

    public override void CleanUpBulkInsert(string tableName) { }

    public override void BeforeBulkInsert(string tableName) { }

    public override void AfterBulkInsert(string tableName) { }

    public override IConnectionManager Clone() => throw new NotSupportedException();
}

/// <summary>
/// A user's own <see cref="DbConnectionManager{TConnection}"/> subclass, for example a wrapper around
/// <c>NpgsqlConnection</c>. It reports its engine itself and supplies no type conventions of its own
/// unless a subclass does.
/// </summary>
internal class CustomDbConnectionManager(ConnectionManagerType engine)
    : DbConnectionManager<SqliteConnection>
{
    public override ConnectionManagerType ConnectionManagerType => engine;

    public override string QB => "\"";

    public override string QE => "\"";

    public override CultureInfo ConnectionCulture => CultureInfo.InvariantCulture;

    public override void BulkInsert(ITableData data, string tableName) { }

    public override void PrepareBulkInsert(string tableName) { }

    public override void CleanUpBulkInsert(string tableName) { }

    public override void BeforeBulkInsert(string tableName) { }

    public override void AfterBulkInsert(string tableName) { }

    public override IConnectionManager Clone() => throw new NotSupportedException();
}

/// <summary>
/// A custom manager that reports PostgreSQL but supplies conventions of its own.
/// </summary>
internal sealed class CustomManagerWithOwnConventions(IDbTypeConventions conventions)
    : CustomDbConnectionManager(ConnectionManagerType.Postgres)
{
    public override IDbTypeConventions TypeConventions => conventions;
}

/// <summary>
/// Conventions that turn every column into <c>TEXT</c>, unlike the rules of any real engine.
/// </summary>
internal sealed class EveryColumnIsTextConventions : IDbTypeConventions
{
    public string GetDbSpecificType(ITableColumn column) => "TEXT";
}
