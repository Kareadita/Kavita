using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Services;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.ReadingList;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

/// <summary>
/// v0.9.2 - Reading list and collection covers built from their items are now stored and refreshed by a recurring task.
/// Reading list covers Kavita built before are handed to that task, and every list and collection without a cover gets one
/// </summary>
// Static BackgroundJob throws here: JobStorage.Current is only set once DI first resolves Hangfire's storage
public class ManualMigrateCompositeCovers(IBackgroundJobClient backgroundJobClient) : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateCompositeCovers);

    protected override async Task ExecuteAsync(DataContext context, ILogger logger)
    {
        // A CBL import's cover came from the CBL and is not rebuilt from its items
        var builtRowIds = context.ReadingList
            .Where(r => !r.CoverImageLocked && r.Provider == ReadingListProvider.None && r.CoverImageFileId != null)
            .Select(r => r.CoverImageFileId);

        var marked = await context.ImageFile
            .Where(f => f.Origin == ImageOrigin.Legacy && builtRowIds.Contains(f.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Origin, ImageOrigin.Composite));

        logger.LogInformation("Marked {Count} reading list covers as built from their items", marked);

        backgroundJobClient.Enqueue<ICompositeCoverService>(s => s.CreateMissingCoversAsync(CancellationToken.None));
    }
}
