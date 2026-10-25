using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

/// <summary>
/// v0.9.2 - Folder watching was seeded off, so most servers never had it on. Each library's own mode now decides
/// </summary>
public class ManualMigrateFolderWatchingOn : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateFolderWatchingOn);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var updated = await context.ServerSetting
            .Where(s => s.Key == ServerSettingKey.EnableFolderWatching && s.Value != "true")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "true"));

        if (updated > 0)
        {
            logger.LogCritical("Turned on Folder Watching, each library's mode now decides whether it watches");
        }
    }
}
