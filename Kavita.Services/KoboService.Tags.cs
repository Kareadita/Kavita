using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.User;
using Kavita.Services.Kobo;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services;

public partial class KoboService
{
    /// <summary>
    /// Appends NewTag / ChangedTag / DeletedTag for visible Reading Lists and Collections.
    /// Not limited by sync page size. Advances and returns the tags watermark.
    /// </summary>
    private async Task<DateTime> AppendTagDeltasAsync(int userId, IReadOnlyCollection<int> libraryIds,
        JsonArray items, DateTime tagsWatermark, CancellationToken ct)
    {
        var newWatermark = tagsWatermark;

        var deletedMax = await AppendDeletedTagsAsync(userId, items, ct);
        if (deletedMax > newWatermark) newWatermark = deletedMax;

        var lists = await unitOfWork.DataContext.ReadingList
            .AsNoTracking()
            .Where(l => l.AppUserId == userId || l.Promoted)
            .Include(l => l.Items)
            .OrderBy(l => l.LastModifiedUtc)
            .ThenBy(l => l.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var list in lists)
        {
            var created = UtcOrUnspecified(list.CreatedUtc == default ? list.Created : list.CreatedUtc);
            var modified = UtcOrUnspecified(list.LastModifiedUtc == default ? list.LastModified : list.LastModifiedUtc);
            if (created <= tagsWatermark && modified <= tagsWatermark) continue;

            var tagId = KoboTagId.FromReadingListIdString(list.Id);
            var itemUuids = await ResolveReadingListItemUuidsAsync(list, libraryIds, ct);
            var envelopeKey = created > tagsWatermark ? "NewTag" : "ChangedTag";
            items.Add(new JsonObject
            {
                [envelopeKey] = new JsonObject
                {
                    ["Tag"] = BuildTagPayload(tagId, list.Title, created, modified, itemUuids),
                },
            });

            if (modified > newWatermark) newWatermark = modified;
            if (created > newWatermark) newWatermark = created;
        }

        var collections = await unitOfWork.DataContext.AppUserCollection
            .AsNoTracking()
            .Where(c => c.AppUserId == userId || c.Promoted)
            .Include(c => c.Items)
            .OrderBy(c => c.LastModifiedUtc)
            .ThenBy(c => c.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var collection in collections)
        {
            var created = UtcOrUnspecified(
                collection.CreatedUtc == default ? collection.Created : collection.CreatedUtc);
            var modified = UtcOrUnspecified(
                collection.LastModifiedUtc == default ? collection.LastModified : collection.LastModifiedUtc);
            if (created <= tagsWatermark && modified <= tagsWatermark) continue;

            var tagId = KoboTagId.FromCollectionIdString(collection.Id);
            var itemUuids = await ResolveCollectionItemUuidsAsync(collection, libraryIds, ct);
            var envelopeKey = created > tagsWatermark ? "NewTag" : "ChangedTag";
            items.Add(new JsonObject
            {
                [envelopeKey] = new JsonObject
                {
                    ["Tag"] = BuildTagPayload(tagId, collection.Title, created, modified, itemUuids),
                },
            });

            if (modified > newWatermark) newWatermark = modified;
            if (created > newWatermark) newWatermark = created;
        }

        // Bump past last emitted stamp so float epoch round-trips cannot re-select the same tags.
        if (newWatermark > tagsWatermark)
        {
            newWatermark = newWatermark.AddMilliseconds(1);
        }

        return newWatermark;
    }

    private async Task<DateTime> AppendDeletedTagsAsync(int userId, JsonArray items, CancellationToken ct)
    {
        var tombstones = await unitOfWork.DataContext.AppUserKoboTagTombstone
            .Where(t => t.AppUserId == userId)
            .OrderBy(t => t.LastModifiedUtc)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);
        if (tombstones.Count == 0) return DateTime.MinValue;

        var maxModified = DateTime.MinValue;
        foreach (var tombstone in tombstones)
        {
            var modified = UtcOrUnspecified(tombstone.LastModifiedUtc);
            items.Add(new JsonObject
            {
                ["DeletedTag"] = new JsonObject
                {
                    ["Tag"] = new JsonObject
                    {
                        ["Id"] = tombstone.TagId.ToString(),
                        ["LastModified"] = FormatKoboTimestamp(modified),
                    },
                },
            });
            if (modified > maxModified) maxModified = modified;
            unitOfWork.DataContext.AppUserKoboTagTombstone.Remove(tombstone);
        }

        return maxModified;
    }

    private async Task<List<string>> ResolveReadingListItemUuidsAsync(ReadingList list,
        IReadOnlyCollection<int> libraryIds, CancellationToken ct)
    {
        var orderedChapterIds = list.Items
            .OrderBy(i => i.Order)
            .ThenBy(i => i.Id)
            .Select(i => i.ChapterId)
            .ToList();
        if (orderedChapterIds.Count == 0) return [];

        var eligible = await EligibleChaptersQuery(libraryIds)
            .Where(c => orderedChapterIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);
        var eligibleSet = eligible.ToHashSet();

        return orderedChapterIds
            .Where(eligibleSet.Contains)
            .Select(KoboEntitlementId.FromChapterIdString)
            .ToList();
    }

    private async Task<List<string>> ResolveCollectionItemUuidsAsync(AppUserCollection collection,
        IReadOnlyCollection<int> libraryIds, CancellationToken ct)
    {
        var seriesIds = collection.Items.Select(s => s.Id).ToHashSet();
        if (seriesIds.Count == 0) return [];

        var chapterIds = await EligibleChaptersQuery(libraryIds)
            .Where(c => seriesIds.Contains(c.Volume.SeriesId))
            .OrderBy(c => c.Volume.SeriesId)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .Select(c => c.Id)
            .ToListAsync(ct);

        return chapterIds.Select(KoboEntitlementId.FromChapterIdString).ToList();
    }

    private static JsonObject BuildTagPayload(string tagId, string name, DateTime created, DateTime modified,
        IReadOnlyList<string> itemUuids)
    {
        var items = new JsonArray();
        foreach (var uuid in itemUuids)
        {
            items.Add(new JsonObject
            {
                ["RevisionId"] = uuid,
                ["Type"] = "ProductRevisionTagItem",
            });
        }

        return new JsonObject
        {
            ["Created"] = FormatKoboTimestamp(created),
            ["Id"] = tagId,
            ["Items"] = items,
            ["LastModified"] = FormatKoboTimestamp(modified),
            ["Name"] = name,
            ["Type"] = "UserTag",
        };
    }

    private static DateTime UtcOrUnspecified(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
