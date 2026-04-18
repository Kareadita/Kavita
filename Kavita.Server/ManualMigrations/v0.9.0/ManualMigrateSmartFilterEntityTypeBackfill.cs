using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Services.Helpers.SmartFilter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._0;

/// <summary>
/// v0.9.0 expanded the Smart Filter to be multi-entity and thus all existing SmartFilters need to be updated
/// </summary>
public class ManualMigrateSmartFilterEntityTypeBackfill : ManualMigration
{
    protected override string MigrationName => "ManualMigrateSmartFilterEntityTypeBackfill";
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        // Find all Smart Filters and re-encode them so they have the entityType on
        var filters = await context.AppUserSmartFilter.ToListAsync();
        if (filters.Count == 0) return;

        foreach (var filter in filters)
        {
            var decodedFilter = SmartFilterHelper.Decode(filter.Filter);
            if (decodedFilter is SeriesFilterV2Dto dto)
            {
                filter.Filter = SmartFilterHelper.Encode(dto);
            }
        }

        await context.SaveChangesAsync();

    }
}
