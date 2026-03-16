using System;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Database;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._8._9;

/// <summary>
/// v0.8.9 - Migrating from fixed api key to user-defined with configurable length
/// </summary>
public class MigrateToAuthKeys : ManualMigration
{
    protected override string MigrationName => nameof(MigrateToAuthKeys);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        // First: Migrate all existing ApiKeys
        var allUsers = await context.AppUser
            .Include(u => u.AuthKeys)
            .ToListAsync();

        foreach (var user in allUsers)
        {
            if (user.AuthKeys.Count != 0) continue;

            var key = new AppUserAuthKey()
            {
                Name = AuthKeyHelper.OpdsKeyName,
                Key = user.ApiKey,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = null,
                Provider = AuthKeyProvider.System,
            };

            user.AuthKeys.Add(key);

            var imageKey = new AppUserAuthKey()
            {
                Name = AuthKeyHelper.ImageOnlyKeyName,
                Key = AuthKeyHelper.GenerateKey(16),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = null,
                Provider = AuthKeyProvider.System,
            };

            user.AuthKeys.Add(imageKey);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }
}
