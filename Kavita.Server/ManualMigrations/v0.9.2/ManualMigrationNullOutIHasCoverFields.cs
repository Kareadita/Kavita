using System;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

#pragma warning disable CS0618

/// <summary>
/// Null out all the CoverImage/PrimaryColor/SecondaryColor fields on all entities that had IHasCoverImage. It was too complex
/// to remove the fields from the DB and keep migrations working.
/// </summary>
public class ManualMigrationNullOutIHasCoverFields : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationNullOutIHasCoverFields);
    protected override async Task ExecuteAsync(DataContext context, ILogger logger)
    {
        await NullOut(nameof(context.Library), logger, () => context.Library.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null)));

        await NullOut(nameof(context.ReadingList), logger, () => context.ReadingList.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null)));

        await NullOut(nameof(context.AppUser), logger, () => context.AppUser.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null)));

        await NullOut(nameof(context.Person), logger, () => context.Person.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null)));

        await NullOut(nameof(context.AppUserCollection), logger, () => context.AppUserCollection.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null)));

        await NullOut(nameof(context.Volume), logger, () => context.Volume.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null)));

        await NullOut(nameof(context.Series), logger, () => context.Series.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null)));

        await NullOut(nameof(context.Chapter), logger, () => context.Chapter.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null)));

        await NullOut(nameof(context.AppUserBookmark), logger, () => context.AppUserBookmark.Where(c => c.FileName != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FileName, (string?) null)));
    }

    private static async Task NullOut(string table, ILogger logger, Func<Task<int>> update)
    {
        try
        {
            var count = await update();
            logger.LogInformation("Cleared the legacy cover image and colors on {Count} {Table} rows", count, table);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not clear the legacy cover image and colors on {Table}, the old values stay but nothing reads them", table);
        }
    }
}
