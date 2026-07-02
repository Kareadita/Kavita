using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// Early Relationship metadata audit entries (pre-v0.9.0.12) stored the relation kind as a string and did not include the related
/// series' library id (needed to render a navigable link in the UI). This backfills the library id and rewrites the
/// kind as the numeric <see cref="RelationKind"/> so the UI can localize it.
/// </summary>
public class ManualMigrateRelationshipAuditHistory : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateRelationshipAuditHistory);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var logs = await context.KavitaPlusAuditLogs
            .Where(l => l.Category == KavitaPlusAuditCategory.Metadata && l.Payload != null)
            .ToListAsync();
        if (logs.Count == 0) return;

        var libraryIdBySeries = await context.Series
            .ToDictionaryAsync(s => s.Id, s => s.LibraryId);

        var modified = 0;
        foreach (var log in logs)
        {
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(log.Payload!);
            }
            catch
            {
                // Not valid JSON / unexpected shape, leave it untouched
                continue;
            }

            // Other Metadata-category payloads (covers, people) aren't change-sets, so guard the shape
            if (root is not JsonObject rootObj || rootObj["Changes"] is not JsonArray changes) continue;

            var changedHere = false;
            foreach (var change in changes)
            {
                if (change is not JsonObject changeObj) continue;
                // MetadataFieldChangeKind.Relationships == 1
                if (changeObj["Field"]?.GetValue<int>() != 1) continue;
                if (changeObj["To"] is not JsonArray relations) continue;

                foreach (var rel in relations)
                {
                    if (rel is not JsonObject relObj) continue;

                    // kind: string ("Prequel") -> int (1)
                    if (relObj["kind"] is JsonValue kindValue && kindValue.GetValueKind() == JsonValueKind.String
                        && Enum.TryParse<RelationKind>(kindValue.GetValue<string>(), out var kind))
                    {
                        relObj["kind"] = (int) kind;
                        changedHere = true;
                    }

                    // Backfill relatedSeriesLibraryId from the related series id
                    if (relObj["relatedSeriesLibraryId"] == null
                        && relObj["relatedSeriesId"] is JsonValue idValue
                        && idValue.GetValueKind() == JsonValueKind.Number)
                    {
                        var seriesId = idValue.GetValue<int>();
                        relObj["relatedSeriesLibraryId"] = libraryIdBySeries.TryGetValue(seriesId, out var libId) ? libId : 0;
                        changedHere = true;
                    }
                }
            }

            if (!changedHere) continue;

            log.Payload = root.ToJsonString();
            modified++;
        }

        if (modified > 0)
        {
            await context.SaveChangesAsync();
        }

        logger.LogInformation("[ManualMigrateRelationshipAuditHistory] Updated {Count} relationship audit log payload(s)", modified);
    }
}
