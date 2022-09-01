using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data;

/// <summary>
/// v0.5.6 introduced Normalized Localized Name, which allows for faster lookups and less memory usage. This migration will calculate them once
/// </summary>
public static class MigrateNormalizedLocalizedName
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        if (!await dataContext.Series.Where(s => s.NormalizedLocalizedName == null).AnyAsync())
        {
            return;
        }
        logger.LogInformation("Running MigrateNormalizedLocalizedName migration. Please be patient, this may take some time");


        foreach (var series in await dataContext.Series.ToListAsync())
        {
            series.NormalizedLocalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(series.LocalizedName ?? string.Empty);
            logger.LogInformation("Updated {SeriesName} normalized localized name: {LocalizedName}", series.Name, series.NormalizedLocalizedName);
            unitOfWork.SeriesRepository.Update(series);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }

        logger.LogInformation("MigrateNormalizedLocalizedName migration finished");

    }

}
