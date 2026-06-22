using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Repositories;
using Kavita.API.Services.Plus;
using Kavita.Database;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

public class ManualMigrationScrobbleRework: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationScrobbleRework);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var librariesWithScrobbleEnables = await context.Library
            .Where(l => l.AllowScrobbling)
            .Select(l => l.Id)
            .ToListAsync();

        var users = await context.AppUser
            .Includes(AppUserIncludes.UserPreferences)
            .ToListAsync();

        List<int> usersToSync = [];

        foreach (var user in users)
        {
            logger.LogTrace("Migrating scrobble info for user {UserName}", user.UserName);

            user.ScrobbleProviders[ScrobbleProvider.AniList] = new AppUserScrobbleProvider
            {
                Provider = ScrobbleProvider.AniList,
                AuthenticationToken = user.AniListAccessToken,
                Settings = new ScrobbleProviderSettingsDto()
                {
                    ProgressScrobbling = user.UserPreferences.AniListScrobblingEnabled,
                    WantToReadSync = user.UserPreferences.WantToReadSync,
                    Libraries = librariesWithScrobbleEnables
                },
                ScrobbleEventGenerationRan = user.ScrobbleEventGenerationRan,
            };
            user.ScrobbleProviders[ScrobbleProvider.Mal] = new AppUserScrobbleProvider
            {
                Provider = ScrobbleProvider.Mal,
                AuthenticationToken = user.MalAccessToken,
                UserName = user.MalUserName,
                Settings = new ScrobbleProviderSettingsDto() {
                    WantToReadSync = user.UserPreferences.WantToReadSync,
                    Libraries = librariesWithScrobbleEnables
                }
            };

            context.AppUser.Update(user);
            context.AppUserPreferences.Update(user.UserPreferences);

            if (!string.IsNullOrEmpty(user.AniListAccessToken))
                usersToSync.Add(user.Id);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        foreach (var userId in usersToSync)
        {
            BackgroundJob.Enqueue<IScrobblingService>(s => s.SyncProviderInfo(userId, ScrobbleProvider.AniList, CancellationToken.None));
        }

        await context.ScrobbleEvent
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.ScrobbleProvider, ScrobbleProvider.AniList));
    }
}
