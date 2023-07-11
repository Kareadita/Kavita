using System.Linq;
using System.Threading.Tasks;
using API.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.7.4 introduced Scrobbling with Kavita+. By default, it is on, but Comic libraries have no scrobble providers, so disable
/// </summary>
public static class MigrateDisableScrobblingOnComicLibraries
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        if (!await dataContext.Library.Where(s => s.Type == LibraryType.Comic).Where(l => l.AllowScrobbling).AnyAsync())
        {
            return;
        }
        logger.LogInformation("Running MigrateDisableScrobblingOnComicLibraries migration. Please be patient, this may take some time");


        foreach (var lib in await dataContext.Library.Where(s => s.Type == LibraryType.Comic).Where(l => l.AllowScrobbling).ToListAsync())
        {
            lib.AllowScrobbling = false;
            unitOfWork.LibraryRepository.Update(lib);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }

        logger.LogInformation("MigrateDisableScrobblingOnComicLibraries migration finished");

    }

}
