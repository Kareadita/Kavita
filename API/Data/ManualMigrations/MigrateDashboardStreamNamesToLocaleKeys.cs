using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.7.8.6 explicitly introduced DashboardStream and v0.7.8.9 changed the default seed titles to use locale strings.
/// This migration will target nightly releases and should be removed before v0.7.9 release.
/// </summary>
public static class MigrateDashboardStreamNamesToLocaleKeys
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        var allStreams = await unitOfWork.UserRepository.GetAllDashboardStreams();
        if (!allStreams.Any(s => s.Name.Equals("On Deck"))) return;

        logger.LogCritical("Running MigrateDashboardStreamNamesToLocaleKeys migration. Please be patient, this may take some time depending on the size of your library. Do not abort, this can break your Database");
        foreach (var stream in allStreams.Where(s => s.IsProvided))
        {
            stream.Name = stream.Name switch
            {
                "On Deck" => "on-deck",
                "Recently Updated" => "recently-updated",
                "Newly Added" => "newly-added",
                "More In" => "more-in-genre",
                _ => stream.Name
            };
            unitOfWork.UserRepository.Update(stream);
        }

        await unitOfWork.CommitAsync();
        logger.LogInformation("MigrateDashboardStreamNamesToLocaleKeys migration finished");
    }
}
