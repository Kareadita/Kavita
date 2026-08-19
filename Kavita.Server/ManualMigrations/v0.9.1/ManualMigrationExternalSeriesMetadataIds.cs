using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// v0.9.1 introduced more metadata providers and shifted the Id store to the <see cref="Series"/>. Map the AL/MAL ids over
/// </summary>
public class ManualMigrationExternalSeriesMetadataIds: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationExternalSeriesMetadataIds);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var aniListCount = await context.Series
            .Where(s => s.AniListId == 0)
            .Where(s => context.ExternalSeriesMetadata.Any(m => m.SeriesId == s.Id && m.AniListId > 0))
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.AniListId,
                x => context.ExternalSeriesMetadata
                    .Where(m => m.SeriesId == x.Id)
                    .Select(m => m.AniListId)
                    .FirstOrDefault()));

        var malCount = await context.Series
            .Where(s => s.MalId == 0)
            .Where(s => context.ExternalSeriesMetadata.Any(m => m.SeriesId == s.Id && m.MalId > 0))
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.MalId,
                x => context.ExternalSeriesMetadata
                    .Where(m => m.SeriesId == x.Id)
                    .Select(m => m.MalId)
                    .FirstOrDefault()));

        var mangaBakaCount = await context.Series
            .Where(s => s.MangaBakaId == 0)
            .Where(s => context.ExternalSeriesMetadata.Any(m => m.SeriesId == s.Id && m.MangabakaId > 0))
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.MangaBakaId,
                x => context.ExternalSeriesMetadata
                    .Where(m => m.SeriesId == x.Id)
                    .Select(m => m.MangabakaId)
                    .FirstOrDefault()));

        var hardcoverCount = await context.Series
            .Where(s => s.HardcoverId == 0)
            .Where(s => context.ExternalSeriesMetadata.Any(m => m.SeriesId == s.Id && m.HardcoverId > 0))
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.HardcoverId,
                x => context.ExternalSeriesMetadata
                    .Where(m => m.SeriesId == x.Id)
                    .Select(m => m.HardcoverId)
                    .FirstOrDefault()));

        var cbrCount = await context.Series
            .Where(s => s.CbrId == 0)
            .Where(s => context.ExternalSeriesMetadata.Any(m => m.SeriesId == s.Id && m.CbrId > 0))
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.CbrId,
                x => context.ExternalSeriesMetadata
                    .Where(m => m.SeriesId == x.Id)
                    .Select(m => m.CbrId)
                    .FirstOrDefault()));

        var totalCount = aniListCount + malCount + mangaBakaCount + hardcoverCount + cbrCount;
        logger.LogInformation("Updated external ids for {Count} series",totalCount);
    }
}
