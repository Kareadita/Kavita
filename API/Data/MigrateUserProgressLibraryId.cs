using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace API.Data;

/// <summary>
/// Introduced in v0.6.1.8 and v0.7, this adds library ids to all User Progress to allow for easier queries against progress
/// </summary>
public static class MigrateUserProgressLibraryId
{
    public static async Task Migrate(IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        logger.LogCritical("Running MigrateUserProgressLibraryId migration - Please be patient, this may take some time. This is not an error");

        var progress = await unitOfWork.AppUserProgressRepository.GetAnyProgress();
        if (progress == null || progress.LibraryId != 0)
        {
            logger.LogCritical("Running MigrateUserProgressLibraryId migration - complete. Nothing to do");
            return;
        }

        var seriesIdsWithLibraryIds = await unitOfWork.SeriesRepository.GetLibraryIdsForSeriesAsync();
        foreach (var prog in await unitOfWork.AppUserProgressRepository.GetAllProgress())
        {
            prog.LibraryId = seriesIdsWithLibraryIds[prog.SeriesId];
            unitOfWork.AppUserProgressRepository.Update(prog);
        }


        await unitOfWork.CommitAsync();

        logger.LogCritical("Running MigrateSeriesRelationsImport migration - Completed. This is not an error");
    }
}
