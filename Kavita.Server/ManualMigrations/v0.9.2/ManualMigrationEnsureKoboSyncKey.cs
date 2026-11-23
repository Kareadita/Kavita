using System.Linq;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Database;
using Kavita.Models;
using Kavita.Models.Entities.Enums.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

public class ManualMigrationEnsureKoboSyncKey: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationEnsureKoboSyncKey);
    protected override async Task ExecuteAsync(DataContext context, ILogger logger)
    {
        var users = await context.AppUser
            .Include(u => u.AuthKeys)
            .ToListAsync();

        foreach (var user in users)
        {
            var koboSyncKey = user.AuthKeys
                .FirstOrDefault(k => k.Name == AuthKeyHelper.KoboKeyName);
            if (koboSyncKey is null)
            {
                user.AuthKeys.Add(Defaults.CreateDefaultAuthKeys().First(k => k.Name == AuthKeyHelper.KoboKeyName));
            }
        }

        await context.SaveChangesAsync();
    }
}
