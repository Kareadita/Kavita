using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Server;
using Kavita.Server.ManualMigrations;
using Kavita.Services.Helpers.SmartFilter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

public class ManualMigrateLastReadDates: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateLastReadDates);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var allSeriesSmartFilters = await context.AppUserSmartFilter
            .Where(f => f.EntityType == FilterEntityType.Series)
            .ToListAsync();

        var updatedCount = 0;

        foreach (var filter in allSeriesSmartFilters)
        {
            var decodedFilter = (SeriesFilterV2Dto) SmartFilterHelper.Decode(filter.Filter);
            foreach (var statement in decodedFilter.Statements.Where(s => s.Field == SeriesFilterField.ReadLast))
            {
                statement.Comparison = statement.Comparison switch
                {
                    FilterComparison.GreaterThan => FilterComparison.LessThan,
                    FilterComparison.GreaterThanEqual => FilterComparison.LessThanEqual,
                    FilterComparison.LessThan => FilterComparison.GreaterThan,
                    FilterComparison.LessThanEqual => FilterComparison.GreaterThanEqual,
                    _ => statement.Comparison
                };
            }

            var encodedFilter = SmartFilterHelper.Encode(decodedFilter);
            if (encodedFilter != filter.Filter)
            {
                filter.Filter = encodedFilter;
                context.AppUserSmartFilter.Update(filter);
                updatedCount++;
            }
        }

        await context.SaveChangesAsync();

        logger.LogInformation("[ManualMigrateLastReadDates] Updated {Count} smart filter(s)", updatedCount);
    }
}
