using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using API.Entities.Enums;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data;

internal sealed class SeriesRelationMigrationOutput
{
    public string SeriesName { get; set; }
    public int SeriesId { get; set; }
    public string TargetSeriesName { get; set; }
    public int TargetId { get; set; }
    public RelationKind Relationship { get; set; }
}

/// <summary>
/// Introduced in v0.6.1.2 and v0.7, this exports to a temp file the existing series relationships. It is a 3 part migration.
/// This will run first, to export the data, then the DB migration will change the way the DB is constructed, then the last migration
/// will import said file and re-construct the relationships.
/// </summary>
public static class MigrateSeriesRelationsExport
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger)
    {
        // TODO: Put a version check in here
        if (new FileInfo("config/temp/relations.csv").Exists)
        {
            return;
        }

        logger.LogCritical("Running MigrateSeriesRelationsExport migration - Please be patient, this may take some time. This is not an error");

        var seriesWithRelationships = await dataContext.Series
            .Where(s => s.Relations.Any())
            .Include(s => s.Relations)
            .ThenInclude(r => r.TargetSeries)
            .AsNoTracking()
            .ToListAsync();

        var records = new List<SeriesRelationMigrationOutput>();
        var excludedRelationships = new List<RelationKind>()
        {
            RelationKind.Parent,
        };
        foreach (var series in seriesWithRelationships)
        {
            foreach (var relationship in series.Relations.Where(r => !excludedRelationships.Contains(r.RelationKind)))
            {
                records.Add(new SeriesRelationMigrationOutput()
                {
                    SeriesId = series.Id,
                    SeriesName = series.Name,
                    Relationship = relationship.RelationKind,
                    TargetId = relationship.TargetSeriesId,
                    TargetSeriesName = relationship.TargetSeries.Name

                });
            }
        }

        await using var writer = new StreamWriter("config/temp/relations.csv");
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            await csv.WriteRecordsAsync(records);
        }

        logger.LogCritical("Running MigrateSeriesRelationsExport migration - Completed. This is not an error");
    }
}
