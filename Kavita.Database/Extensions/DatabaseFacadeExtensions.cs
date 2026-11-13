using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kavita.Database.Extensions;

public static class DatabaseFacadeExtensions
{
    public static async Task<DbJournalMode> GetJournalModeAsync(this DatabaseFacade database, CancellationToken ct = default)
    {
        await database.OpenConnectionAsync(ct);
        try
        {
            await using var command = database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            return Enum.Parse<DbJournalMode>((string) (await command.ExecuteScalarAsync(ct))!, ignoreCase: true);
        }
        finally
        {
            await database.CloseConnectionAsync();
        }
    }
}
