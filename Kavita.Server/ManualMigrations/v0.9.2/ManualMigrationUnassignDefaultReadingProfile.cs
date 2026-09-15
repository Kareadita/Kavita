using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

public class ManualMigrationUnassignDefaultReadingProfile: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationUnassignDefaultReadingProfile);
    protected override Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        return context.AppUserReadingProfiles
            .Where(p => p.Kind == ReadingProfileKind.Default)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.SeriesIds, [])
                .SetProperty(p => p.LibraryIds, []));
    }
}
