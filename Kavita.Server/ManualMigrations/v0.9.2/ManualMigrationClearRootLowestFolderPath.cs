using System.Linq;
using System.Threading.Tasks;
using Kavita.Common.Extensions;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

/// <summary>
/// A series with files in two top level folders of a drive root library stored the drive as its LowestFolderPath,
/// which made every scan walk the entire library
/// </summary>
public class ManualMigrationClearRootLowestFolderPath : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationClearRootLowestFolderPath);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var series = await context.Series
            .Where(s => s.LowestFolderPath != null)
            .Select(s => new
            {
                s.Id,
                s.LowestFolderPath,
                LibraryFolders = s.Library.Folders.Select(f => f.Path).ToList(),
            })
            .ToListAsync();

        var outsideLibrary = series
            .Where(s => !s.LibraryFolders.Any(s.LowestFolderPath.IsInsideFolder))
            .Select(s => s.Id)
            .ToList();

        if (outsideLibrary.Count == 0) return;

        await context.Series
            .Where(s => outsideLibrary.Contains(s.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LowestFolderPath, (string?) null));

        logger.LogInformation("Cleared LowestFolderPath on {Count} series where it was not inside the library folder", outsideLibrary.Count);
    }
}
