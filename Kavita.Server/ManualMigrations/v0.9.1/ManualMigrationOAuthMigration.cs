using System;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

public class ManualMigrationOAuthMigration: ManualMigration
{
    protected override string MigrationName { get; } = nameof(ManualMigrationOAuthMigration);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        logger.LogWarning("Removing connection details for MangaBaka and Mal, users will need to connect again via the OAuth flow.");

        var users = await context.AppUser.ToListAsync();

        foreach (var user in users)
        {
            user.ScrobbleProviders[ScrobbleProvider.MangaBaka].UserName = string.Empty;
            user.ScrobbleProviders[ScrobbleProvider.MangaBaka].AuthenticationToken = string.Empty;
            user.ScrobbleProviders[ScrobbleProvider.MangaBaka].ValidUntilUtc = DateTime.MinValue;

            user.ScrobbleProviders[ScrobbleProvider.Mal].UserName = string.Empty;
            user.ScrobbleProviders[ScrobbleProvider.Mal].AuthenticationToken = string.Empty;
            user.ScrobbleProviders[ScrobbleProvider.Mal].ValidUntilUtc = DateTime.MinValue;

            context.AppUser.Update(user);
        }

        await context.SaveChangesAsync();
    }
}
