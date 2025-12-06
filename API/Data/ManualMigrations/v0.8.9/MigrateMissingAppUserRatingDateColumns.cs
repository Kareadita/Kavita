using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.9 - AppUserRating is missing Created/CreatedUtc/LastModified/LastModifiedUtc on old installs
/// </summary>
public class MigrateMissingAppUserRatingDateColumns : ManualMigration
{
    protected override string MigrationName => nameof(MigrateMissingAppUserRatingDateColumns );

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        logger.LogDebug("Checking for missing date columns on AppUserRating table");

        // Check which columns are missing
        var missingColumns = await GetMissingColumnsAsync(context);

        if (missingColumns.Count == 0)
        {
            logger.LogDebug("All date columns already exist on AppUserRating table. Skipping migration.");
            return;
        }

        logger.LogDebug("Missing columns: {Columns}", string.Join(", ", missingColumns));

        // Add missing columns
        foreach (var column in missingColumns)
        {
            logger.LogDebug("Adding column {Column} to AppUserRating", column);
            await AddColumnAsync(context, column);
        }

        // Backfill only the columns we just added
        await BackfillDateColumnsAsync(context, logger, missingColumns);

        logger.LogDebug("Migration completed successfully");
    }

    private static async Task<List<string>> GetMissingColumnsAsync(DataContext dataContext)
    {
        var requiredColumns = new[] { "Created", "CreatedUtc", "LastModified", "LastModifiedUtc" };
        var missingColumns = new List<string>();

        foreach (var column in requiredColumns)
        {
            var exists = await ColumnExistsAsync(dataContext, column);
            if (!exists)
            {
                missingColumns.Add(column);
            }
        }

        return missingColumns;
    }

    private static async Task<bool> ColumnExistsAsync(DataContext dataContext, string columnName)
    {
        var sql = "SELECT COUNT(*) FROM pragma_table_info('AppUserRating') WHERE name = @columnName";
        var connection = dataContext.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@columnName", columnName));

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static async Task AddColumnAsync(DataContext dataContext, string columnName)
    {
        var sql = $"ALTER TABLE AppUserRating ADD COLUMN {columnName} TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'";
        await dataContext.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task BackfillDateColumnsAsync(DataContext dataContext, ILogger<Program> logger, List<string> columnsToBackfill)
    {
        if (columnsToBackfill.Count == 0)
        {
            return;
        }

        logger.LogDebug("Backfilling newly added date columns with current timestamp");

        // Build dynamic SET clause only for columns we just added
        var setClause = string.Join(", ", columnsToBackfill.Select(c => $"{c} = datetime('now')"));

        var updateSql = $@"
            UPDATE AppUserRating
            SET {setClause}
            WHERE {columnsToBackfill[0]} = '0001-01-01 00:00:00'";

        var rowsAffected = await dataContext.Database.ExecuteSqlRawAsync(updateSql);
        logger.LogDebug("Updated {Rows} records", rowsAffected);
    }
}
