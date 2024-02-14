using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// Introduced in v0.7.8.7 and v0.7.9, this adds SideNavStream's for all Libraries a User has access to
/// </summary>
public static class MigrateUserLibrarySideNavStream
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateUserLibrarySideNavStream"))
        {
            return;
        }

        var usersWithLibraryStreams = await dataContext.AppUser
            .Include(u => u.SideNavStreams)
            .AnyAsync(u => u.SideNavStreams.Count > 0 && u.SideNavStreams.Any(s => s.LibraryId > 0));

        if (usersWithLibraryStreams)
        {
            logger.LogCritical("Running MigrateUserLibrarySideNavStream migration - complete. Nothing to do");
            return;
        }

        logger.LogCritical("Running MigrateUserLibrarySideNavStream migration - Please be patient, this may take some time. This is not an error");

        var users = await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.SideNavStreams);
        foreach (var user in users)
        {
            var userLibraries = await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id);
            foreach (var lib in userLibraries)
            {
                var prevMaxOrder = user.SideNavStreams.Max(s => s.Order);
                user.SideNavStreams.Add(new AppUserSideNavStream()
                {
                    Name = lib.Name,
                    LibraryId = lib.Id,
                    IsProvided = false,
                    Visible = true,
                    StreamType = SideNavStreamType.Library,
                    Order = prevMaxOrder + 1
                });
            }
            unitOfWork.UserRepository.Update(user);
        }

        await unitOfWork.CommitAsync();

        logger.LogCritical("Running MigrateUserLibrarySideNavStream migration - Completed. This is not an error");
    }
}
