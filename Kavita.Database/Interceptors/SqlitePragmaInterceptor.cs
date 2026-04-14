using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kavita.Database.Interceptors;

/// <summary>
/// Applies per-connection PRAGMAs every time a SQLite connection is opened.
/// Runs on every pooled connection - <c>AddDbContextPool</c> reuses <c>DbContext</c> instances
/// but the underlying <c>SqliteConnection</c> can open/close across operations, so a one-shot
/// startup PRAGMA is not sufficient.
///
/// <c>busy_timeout</c> makes SQLite sleep up to the given number of milliseconds waiting for
/// the writer lock rather than returning <c>SQLITE_BUSY</c> immediately. This only helps when
/// paired with IMMEDIATE transactions (see <c>UnitOfWork.CommitAsync</c>); DEFERRED transactions
/// still fail with the non-retriable <c>SQLITE_BUSY_SNAPSHOT</c>.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const int BusyTimeoutMs = 30_000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await ApplyPragmasAsync(connection, cancellationToken);
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs};";
        command.ExecuteNonQuery();
    }

    private static async Task ApplyPragmasAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs};";
        await command.ExecuteNonQueryAsync(ct);
    }
}
