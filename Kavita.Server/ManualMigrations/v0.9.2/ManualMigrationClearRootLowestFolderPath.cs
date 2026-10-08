using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Common.Extensions;
using Kavita.Database;
using Kavita.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

/// <summary>
/// A series with files in two top level folders of a drive root library stored the drive as its LowestFolderPath,
/// which made every scan walk the entire library
/// </summary>
public class ManualMigrationClearRootLowestFolderPath : ManualMigration
{
    private const int BatchSize = 1000;

    protected override string MigrationName => nameof(ManualMigrationClearRootLowestFolderPath);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var libraryFolders = (await context.FolderPath
                .Select(f => new { f.LibraryId, f.Path })
                .ToListAsync())
            .GroupBy(f => f.LibraryId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Path).ToList());

        // Collect every id before updating, the batches page over LowestFolderPath != null and clearing it would shift them
        var outsideLibrary = new List<int>();
        var seriesQuery = context.Series
            .Where(s => s.LowestFolderPath != null)
            .Select(s => new { s.Id, s.LibraryId, s.LowestFolderPath })
            .OrderBy(s => s.Id);

        await foreach (var batch in seriesQuery.BatchToAsyncEnumerable(BatchSize))
        {
            outsideLibrary.AddRange(batch
                .Where(s => !libraryFolders.TryGetValue(s.LibraryId, out var folders) ||
                            !folders.Any(s.LowestFolderPath.IsInsideFolder))
                .Select(s => s.Id));
        }

        if (outsideLibrary.Count == 0) return;

        foreach (var ids in outsideLibrary.Chunk(BatchSize))
        {
            await context.Series
                .Where(s => ids.Contains(s.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LowestFolderPath, (string?) null));
        }

        logger.LogInformation("Cleared LowestFolderPath on {Count} series where it was not inside the library folder", outsideLibrary.Count);
    }
}
