using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._0;

/// <summary>
/// v0.9.0 - Ensure that there are no Admin's with the ReadOnly role
/// </summary>
public class ManualMigrateEnsureNoReadOnlyAdmins : ManualMigration
{
    protected override string MigrationName => "ManualMigrateEnsureNoReadOnlyAdmins";
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var users = await context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u =>
                u.UserRoles.Any(r => r.Role.Name == PolicyConstants.ReadOnlyRole) &&
                u.UserRoles.Any(r => r.Role.Name == PolicyConstants.AdminRole))
            .ToListAsync();

        if (users.Count == 0) return;

        foreach (var user in users)
        {
            var readOnlyAssignments = user.UserRoles
                .Where(ur => ur.Role.Name == PolicyConstants.ReadOnlyRole)
                .ToList();

            context.UserRoles.RemoveRange(readOnlyAssignments);
            logger.LogInformation("[{Scope}] Removed Read Only role from admin user {UserName}",
                MigrationName, user.UserName);
        }

        await context.SaveChangesAsync();
    }
}
