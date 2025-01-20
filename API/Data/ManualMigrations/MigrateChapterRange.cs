using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using API.Extensions;
using API.Helpers.Builders;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.0 changed the range to that it doesn't have filename by default
/// </summary>
public static class MigrateChapterRange
{
    public static async Task Migrate(DataContext dataContext, IUnitOfWork unitOfWork, ILogger<Program> logger)
        {
            if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateChapterRange"))
            {
                return;
            }

            logger.LogCritical(
                "Running MigrateChapterRange migration - Please be patient, this may take some time. This is not an error");

            var chapters = await dataContext.Chapter.ToListAsync();
            foreach (var chapter in chapters)
            {
                if (Parser.MinNumberFromRange(chapter.Range).Is(0.0f))
                {
                    chapter.Range = chapter.GetNumberTitle();
                }
            }


            // Save changes after processing all series
            if (dataContext.ChangeTracker.HasChanges())
            {
                await dataContext.SaveChangesAsync();
            }

            dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
            {
                Name = "MigrateChapterRange",
                ProductVersion = BuildInfo.Version.ToString(),
                RanAt = DateTime.UtcNow
            });

            await dataContext.SaveChangesAsync();
            logger.LogCritical(
                "Running MigrateChapterRange migration - Completed. This is not an error");
        }
}
