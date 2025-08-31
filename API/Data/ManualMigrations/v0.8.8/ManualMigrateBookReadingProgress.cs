using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.8 - Switch existing xpaths saved to a descoped version
/// </summary>
public static class ManualMigrateBookReadingProgress
{
    /// <summary>
    /// Scope from 2023 era before a DOM change
    /// </summary>
    private const string OldScope = "//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/";
    /// <summary>
    /// Scope from post DOM change
    /// </summary>
    private const string NewScope = "//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[3]/";
    /// <summary>
    /// New-descoped prefix
    /// </summary>
    private const string ReplacementScope = "//BODY/DIV[1]";

    public static async Task Migrate(DataContext context, IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateBookReadingProgress"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateBookReadingProgress migration - Please be patient, this may take some time. This is not an error");

        // Disable change tracking so that LastUpdated isn't updated breaking stats
        var originalAutoDetectChanges = context.ChangeTracker.AutoDetectChangesEnabled;
        context.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            var bookProgress = await context.AppUserProgresses
                .Where(p => p.BookScrollId != null &&
                            (p.BookScrollId.StartsWith(OldScope) || p.BookScrollId.StartsWith(NewScope)))
                .AsNoTracking()
                .ToListAsync();


            foreach (var progress in bookProgress)
            {
                if (string.IsNullOrEmpty(progress.BookScrollId)) continue;

                if (progress.BookScrollId.StartsWith(OldScope))
                {
                    progress.BookScrollId = progress.BookScrollId.Replace(OldScope, ReplacementScope);
                    context.AppUserProgresses.Update(progress);
                }
                else if (progress.BookScrollId.StartsWith(NewScope))
                {
                    progress.BookScrollId = progress.BookScrollId.Replace(NewScope, ReplacementScope);
                    context.AppUserProgresses.Update(progress);
                }
            }

            if (unitOfWork.HasChanges())
            {
                await context.SaveChangesAsync();
            }

            var ptocEntries = await context.AppUserTableOfContent
                .Where(p => p.BookScrollId != null &&
                            (p.BookScrollId.StartsWith(OldScope) || p.BookScrollId.StartsWith(NewScope)))
                .AsNoTracking()
                .ToListAsync();

            foreach (var ptoc in ptocEntries)
            {
                if (string.IsNullOrEmpty(ptoc.BookScrollId)) continue;

                if (ptoc.BookScrollId.StartsWith("id", StringComparison.InvariantCultureIgnoreCase)) continue;

                if (ptoc.BookScrollId.StartsWith(OldScope))
                {
                    ptoc.BookScrollId = ptoc.BookScrollId.Replace(OldScope, ReplacementScope);
                    context.AppUserTableOfContent.Update(ptoc);
                }
                else if (ptoc.BookScrollId.StartsWith(NewScope))
                {
                    ptoc.BookScrollId = ptoc.BookScrollId.Replace(NewScope, ReplacementScope);
                    context.AppUserTableOfContent.Update(ptoc);
                }
            }

            if (unitOfWork.HasChanges())
            {
                await context.SaveChangesAsync();
            }
        }
        finally
        {
            // Restore original setting
            context.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetectChanges;
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateBookReadingProgress",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateBookReadingProgress migration - Completed. This is not an error");
    }
}
