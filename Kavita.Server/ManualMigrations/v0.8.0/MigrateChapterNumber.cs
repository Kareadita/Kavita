using System;
using System.Threading.Tasks;
using Kavita.Common.Constants;
using Kavita.Common.EnvironmentInfo;
using Kavita.Database;
using Kavita.Models.Constants;
using Kavita.Models.Entities.History;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._8._0;

/// <summary>
/// Introduced in v0.8.0, this migrates the existing Chapter Range -> Chapter Min/Max Number
/// </summary>
public static class MigrateChapterNumber
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateChapterNumber"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateChapterNumber migration - Please be patient, this may take some time. This is not an error");

        // Get all volumes
        foreach (var chapter in dataContext.Chapter)
        {
            if (chapter.IsSpecial)
            {
                chapter.MinNumber = ParserConstants.DefaultChapterNumber;
                chapter.MaxNumber = ParserConstants.DefaultChapterNumber;
                continue;
            }
            chapter.MinNumber = Parser.MinNumberFromRange(chapter.Range);
            chapter.MaxNumber = Parser.MaxNumberFromRange(chapter.Range);
        }

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateChapterNumber",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });

        await dataContext.SaveChangesAsync();
        logger.LogCritical(
            "Running MigrateChapterNumber migration - Completed. This is not an error");
    }
}
