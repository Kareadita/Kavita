using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

/// <summary>
/// Null out all the CoverImage/PrimaryColor/SecondaryColor fields on all entities that had IHasCoverImage. It was too complex
/// to remove the fields from the DB and keep migrations working.
/// </summary>
public class ManualMigrationNullOutIHasCoverFields : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationNullOutIHasCoverFields);
    protected override async Task ExecuteAsync(DataContext context, ILogger logger)
    {
        await context.Library.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null));

        await context.ReadingList.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null));

        await context.AppUser.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null));

        await context.Person.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null));

        await context.AppUserCollection.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null));

        await context.Volume.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null));

        await context.Series.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null));

        await context.Chapter.Where(c => c.CoverImage != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverImage, (string?) null)
                .SetProperty(c => c.PrimaryColor, (string?) null)
                .SetProperty(c => c.SecondaryColor, (string?) null));
    }
}
