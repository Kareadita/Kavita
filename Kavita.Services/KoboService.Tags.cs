using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.User;
using Kavita.Services.Kobo;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services;

public partial class KoboService
{
    public const string TagNameRequiredMessage = "kobo-tag-name-required";
    public const string TagNotFoundMessage = "kobo-tag-not-found";
    public const string TagForbiddenMessage = "kobo-tag-forbidden";

    public async Task<string> CreateTagAsync(string authToken, JsonObject? body, CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);
        var name = body?["Name"]?.GetValue<string>()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            throw new KavitaException(TagNameRequiredMessage);
        }

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId,
            AppUserIncludes.ReadingListsWithItems, ct);
        if (user == null) throw new KavitaException("access-denied");

        var normalized = name.ToNormalized();
        var existing = user.ReadingLists.FirstOrDefault(l => l.NormalizedTitle == normalized);
        ReadingList list;
        if (existing != null)
        {
            list = existing;
        }
        else
        {
            list = new ReadingListBuilder(name).WithAppUserId(userId).Build();
            user.ReadingLists.Add(list);
            await unitOfWork.CommitAsync(ct);
        }

        await ApplyTagItemsAsync(userId, list, body?["Items"]?.AsArray(), add: true, ct);
        await CommitTagMutationAsync(list, ct);

        return KoboTagId.FromReadingListIdString(list.Id);
    }

    public async Task RenameTagAsync(string authToken, string tagId, JsonObject? body,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);
        var list = await ResolveOwnedReadingListForMutationAsync(userId, tagId, ct);
        var name = body?["Name"]?.GetValue<string>()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            throw new KavitaException(TagNameRequiredMessage);
        }

        if (!string.Equals(list.Title, name, StringComparison.Ordinal))
        {
            var normalized = name.ToNormalized();
            var clash = await unitOfWork.DataContext.ReadingList
                .AnyAsync(l => l.AppUserId == userId && l.Id != list.Id && l.NormalizedTitle == normalized, ct);
            if (clash)
            {
                throw new KavitaException("reading-list-name-exists");
            }

            list.Title = name;
            list.NormalizedTitle = normalized;
            TouchReadingList(list);
            await unitOfWork.CommitAsync(ct);
        }
    }

    public async Task DeleteTagAsync(string authToken, string tagId, CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);
        var list = await ResolveOwnedReadingListForMutationAsync(userId, tagId, ct);

        await PrepareTagDeleteAsync(KoboTagId.FromReadingListId(list.Id), list.AppUserId, list.Promoted, ct);
        unitOfWork.DataContext.ReadingList.Remove(list);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task AddTagItemsAsync(string authToken, string tagId, JsonObject? body,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);
        var list = await ResolveOwnedReadingListForMutationAsync(userId, tagId, ct);
        await ApplyTagItemsAsync(userId, list, body?["Items"]?.AsArray(), add: true, ct);
        await CommitTagMutationAsync(list, ct);
    }

    public async Task RemoveTagItemsAsync(string authToken, string tagId, JsonObject? body,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);
        var list = await ResolveOwnedReadingListForMutationAsync(userId, tagId, ct);
        await ApplyTagItemsAsync(userId, list, body?["Items"]?.AsArray(), add: false, ct);
        await CommitTagMutationAsync(list, ct);
    }

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
            var created = KoboDateTime.CoalesceUtc(list.CreatedUtc, list.Created);
            var modified = KoboDateTime.CoalesceUtc(list.LastModifiedUtc, list.LastModified);
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
            var created = KoboDateTime.CoalesceUtc(collection.CreatedUtc, collection.Created);
            var modified = KoboDateTime.CoalesceUtc(collection.LastModifiedUtc, collection.LastModified);
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
            var modified = KoboDateTime.AsUtc(tombstone.LastModifiedUtc);
            items.Add(new JsonObject
            {
                ["DeletedTag"] = new JsonObject
                {
                    ["Tag"] = new JsonObject
                    {
                        ["Id"] = tombstone.TagId.ToString(),
                        ["LastModified"] = KoboDateTime.FormatTimestamp(modified),
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

        return ToEntitlementUuids(orderedChapterIds.Where(eligibleSet.Contains));
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

        return ToEntitlementUuids(chapterIds);
    }

    /// <summary>Maps chapter ids to their deterministic Kobo entitlement UUID strings.</summary>
    private static List<string> ToEntitlementUuids(IEnumerable<int> chapterIds) =>
        chapterIds.Select(KoboEntitlementId.FromChapterIdString).ToList();

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
            ["Created"] = KoboDateTime.FormatTimestamp(created),
            ["Id"] = tagId,
            ["Items"] = items,
            ["LastModified"] = KoboDateTime.FormatTimestamp(modified),
            ["Name"] = name,
            ["Type"] = "UserTag",
        };
    }


    /// <summary>
    /// Resolves a Tag UUID to an owned Reading List for mutation. Collections and non-owned lists
    /// throw <see cref="TagForbiddenMessage"/>; unknown UUIDs throw <see cref="TagNotFoundMessage"/>.
    /// </summary>
    private async Task<ReadingList> ResolveOwnedReadingListForMutationAsync(int userId, string tagId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(tagId, out var tagGuid))
        {
            throw new KavitaException(TagNotFoundMessage);
        }

        var lists = await unitOfWork.DataContext.ReadingList
            .Include(l => l.Items)
            .Where(l => l.AppUserId == userId || l.Promoted)
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var list in lists)
        {
            if (KoboTagId.FromReadingListId(list.Id) != tagGuid) continue;
            if (list.AppUserId != userId)
            {
                throw new KavitaException(TagForbiddenMessage);
            }

            return list;
        }

        var collectionIds = await unitOfWork.DataContext.AppUserCollection
            .Where(c => c.AppUserId == userId || c.Promoted)
            .Select(c => c.Id)
            .ToListAsync(ct);
        if (collectionIds.Any(id => KoboTagId.FromCollectionId(id) == tagGuid))
        {
            throw new KavitaException(TagForbiddenMessage);
        }

        throw new KavitaException(TagNotFoundMessage);
    }

    private async Task ApplyTagItemsAsync(int userId, ReadingList list, JsonArray? items, bool add,
        CancellationToken ct)
    {
        if (items == null || items.Count == 0) return;

        list.Items ??= new List<ReadingListItem>();
        var existingChapterIds = list.Items.Select(i => i.ChapterId).ToHashSet();
        var nextOrder = list.Items.Count == 0 ? 0 : list.Items.Max(i => i.Order) + 1;
        var changed = false;

        foreach (var itemNode in items)
        {
            if (itemNode is not JsonObject itemObj) continue;
            var type = itemObj["Type"]?.GetValue<string>();
            if (!string.Equals(type, "ProductRevisionTagItem", StringComparison.Ordinal)) continue;

            var revisionId = itemObj["RevisionId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(revisionId)) continue;

            var chapter = await ResolveEligibleChapterAsync(userId, revisionId, ct);
            if (chapter == null) continue;

            if (add)
            {
                if (!existingChapterIds.Add(chapter.Id)) continue;
                list.Items.Add(new ReadingListItemBuilder(nextOrder, chapter.Volume.SeriesId,
                    chapter.VolumeId, chapter.Id).Build());
                nextOrder++;
                changed = true;
            }
            else if (existingChapterIds.Remove(chapter.Id))
            {
                var toRemove = list.Items.Where(i => i.ChapterId == chapter.Id).ToList();
                unitOfWork.ReadingListRepository.BulkRemove(toRemove);
                list.Items = list.Items.Where(i => i.ChapterId != chapter.Id).ToList();
                changed = true;
            }
        }

        if (!add && changed)
        {
            var index = 0;
            foreach (var remaining in list.Items.OrderBy(i => i.Order).ThenBy(i => i.Id))
            {
                remaining.Order = index++;
            }
        }
    }

    private void TouchReadingList(ReadingList list) =>
        unitOfWork.ReadingListRepository.Update(list);

    /// <summary>
    /// Commits a pending tag mutation only when the unit of work has changes, touching the list's
    /// LastModified so the next sync re-emits it. No-op when nothing changed.
    /// </summary>
    private async Task CommitTagMutationAsync(ReadingList list, CancellationToken ct)
    {
        if (!unitOfWork.HasChanges()) return;

        TouchReadingList(list);
        await unitOfWork.CommitAsync(ct);
    }
}
