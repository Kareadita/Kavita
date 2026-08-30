using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// v0.9.1 we received reports of broken CoverImageSizes which broke saving some forms.
/// </summary>
public class ManualMigrateCoverImageSettings: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateCoverImageSettings);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        await context.ServerSetting
            .Where(s => s.Key == ServerSettingKey.CoverImageSize)
            // We've only seen == "0", but let's just catch all values just in case
            .Where(s => s.Value != "1" && s.Value != "2" && s.Value != "3" && s.Value != "4")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Value, "1"));
    }
}
