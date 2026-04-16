using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._0;

/// <summary>
/// v0.9.0 removed the MoreInGenre Dashboard stream type
/// </summary>
public class ManualMigrationRemoveMoreInGenreStream : ManualMigration
{
    protected override string MigrationName => "ManualMigrationRemoveMoreInGenreStream";
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var existing = await context.AppUserDashboardStream
            .Where(s => (int) s.StreamType == 5)
            .ToListAsync();


        context.AppUserDashboardStream.RemoveRange(existing);
        await context.SaveChangesAsync();
    }
}
