using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Builders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Kavita.Database.Tests.Migrations;

public class PdfLinkSettingsMigrationTests
{
    [Fact]
    public async Task MigrateAsync_OnEmptyDatabase_AppliesPdfLinkSettingsWithTrueDefaults()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var context = new DataContext(options);
        await context.Database.MigrateAsync();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains(appliedMigrations, m => m.Contains("PdfLinkSettings"));

        var columnDefaults = await GetLibraryColumnDefaultsAsync(connection);
        Assert.Equal("1", columnDefaults["EnablePdfExternalLinks"]);
        Assert.Equal("1", columnDefaults["EnablePdfInternalLinks"]);

        context.Library.Add(new LibraryBuilder("Migration Test Library")
            .WithFolderPath(new FolderPathBuilder("/data/books").Build())
            .Build());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var library = await context.Library.SingleAsync();
        Assert.True(library.EnablePdfExternalLinks);
        Assert.True(library.EnablePdfInternalLinks);
    }

    private static async Task<Dictionary<string, string>> GetLibraryColumnDefaultsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Library)";

        var columns = new Dictionary<string, string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(1)] = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
        }

        return columns;
    }
}
