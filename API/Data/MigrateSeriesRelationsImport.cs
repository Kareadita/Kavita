using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Entities.Metadata;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data;

/// <summary>
/// Introduced in v0.6.1.2 and v0.7, this imports to a temp file the existing series relationships. It is a 3 part migration.
/// This will run last, to import the data and re-construct the relationships.
/// </summary>
public static class MigrateSeriesRelationsImport
{
    private const string OutputFile = "config/relations.csv";
    private const string CompleteOutputFile = "config/relations-imported.csv";
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger)
    {
        logger.LogCritical("Running MigrateSeriesRelationsImport migration - Please be patient, this may take some time. This is not an error");
        if (!new FileInfo(OutputFile).Exists)
        {
            logger.LogCritical("Running MigrateSeriesRelationsImport migration - complete. Nothing to do");
            return;
        }

        logger.LogCritical("Loading backed up relationships into the DB");
        List<SeriesRelationMigrationOutput> records;
        using var reader = new StreamReader(OutputFile);
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            records = csv.GetRecords<SeriesRelationMigrationOutput>().ToList();
        }

        foreach (var relation in records)
        {
            logger.LogCritical("Importing {SeriesName} --{RelationshipKind}--> {TargetSeriesName}",
                relation.SeriesName, relation.Relationship, relation.TargetSeriesName);

            // Filter out series that don't exist
            if (!await dataContext.Series.AnyAsync(s => s.Id == relation.SeriesId) ||
                !await dataContext.Series.AnyAsync(s => s.Id == relation.TargetId))
                continue;

            await dataContext.SeriesRelation.AddAsync(new SeriesRelation()
            {
                SeriesId = relation.SeriesId,
                TargetSeriesId = relation.TargetId,
                RelationKind = relation.Relationship
            });

        }
        await dataContext.SaveChangesAsync();

        File.Move(OutputFile, CompleteOutputFile);

        logger.LogCritical("Running MigrateSeriesRelationsImport migration - Completed. This is not an error");
    }
}
