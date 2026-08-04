using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Common;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities.User;
using Kavita.Services.Kobo;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services;

/// <summary>
/// Archive / restore / hard-delete surface for Kobo sync: reconciling eligibility changes,
/// device-deleted "removed books", and cross-user hard-delete tombstones.
/// </summary>
public partial class KoboService
{
    public async Task<IReadOnlyList<KoboRemovedBookDto>> GetRemovedBooksAsync(int userId,
        CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.None, ct);
        if (user == null) throw new KavitaException("access-denied");

        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        var deviceArchives = await unitOfWork.DataContext.AppUserKoboArchivedChapter
            .Where(a => a.AppUserId == userId && a.IsDeviceDeleted)
            .ToListAsync(ct);
        if (deviceArchives.Count == 0) return [];

        var archivedIds = deviceArchives.Select(a => a.ChapterId).ToList();
        var eligibleChapters = await EligibleChaptersQuery(libraryIds)
            .Where(c => archivedIds.Contains(c.Id))
            .Include(c => c.Volume)
            .ThenInclude(v => v.Series)
            .ToListAsync(ct);
        if (eligibleChapters.Count == 0) return [];

        var archiveByChapter = deviceArchives.ToDictionary(a => a.ChapterId);
        return eligibleChapters
            .OrderBy(c => c.Volume.Series.Name)
            .ThenBy(c => c.SortOrder)
            .Select(c => new KoboRemovedBookDto
            {
                ChapterId = c.Id,
                SeriesId = c.Volume.SeriesId,
                LibraryId = c.Volume.Series.LibraryId,
                SeriesName = c.Volume.Series.Name,
                Title = KoboEntitlementPayloadBuilder.BuildTitle(c.Volume.Series, c),
                RemovedUtc = archiveByChapter[c.Id].LastModifiedUtc,
            })
            .ToList();
    }

    public async Task RestoreRemovedBooksAsync(int userId, IReadOnlyCollection<int>? chapterIds = null,
        CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.None, ct);
        if (user == null) throw new KavitaException("access-denied");

        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        var deviceArchives = await unitOfWork.DataContext.AppUserKoboArchivedChapter
            .Where(a => a.AppUserId == userId && a.IsDeviceDeleted)
            .ToListAsync(ct);
        if (deviceArchives.Count == 0) return;

        if (chapterIds is { Count: > 0 })
        {
            var requested = chapterIds.ToHashSet();
            deviceArchives = deviceArchives.Where(a => requested.Contains(a.ChapterId)).ToList();
            if (deviceArchives.Count == 0) return;
        }

        var archivedIds = deviceArchives.Select(a => a.ChapterId).ToList();
        var eligibleIds = await EligibleChaptersQuery(libraryIds)
            .Where(c => archivedIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);
        if (eligibleIds.Count == 0) return;

        await ClearArchivesAndUnsyncAsync(userId, eligibleIds, ct);
    }

    public async Task PrepareHardDeleteAsync(IEnumerable<int> chapterIds,
        CancellationToken ct = default)
    {
        var distinctIds = chapterIds.Distinct().ToList();
        if (distinctIds.Count == 0) return;

        var syncedRows = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => distinctIds.Contains(s.ChapterId))
            .ToListAsync(ct);
        if (syncedRows.Count == 0) return;

        var chapters = await unitOfWork.DataContext.Chapter
            .Where(c => distinctIds.Contains(c.Id))
            .Include(c => c.Volume).ThenInclude(v => v.Series)
            .AsSplitQuery()
            .ToListAsync(ct);
        var chapterById = chapters.ToDictionary(c => c.Id);

        var existingTombstones = await unitOfWork.DataContext.AppUserKoboTombstone
            .Where(t => distinctIds.Contains(t.ChapterId))
            .Select(t => new { t.AppUserId, t.ChapterId })
            .ToListAsync(ct);
        var tombstoneKeys = existingTombstones
            .Select(t => (t.AppUserId, t.ChapterId))
            .ToHashSet();

        var now = DateTime.UtcNow;
        foreach (var group in syncedRows.GroupBy(s => s.ChapterId))
        {
            if (!chapterById.TryGetValue(group.Key, out var chapter)) continue;
            var title = KoboEntitlementPayloadBuilder.BuildTitle(chapter.Volume.Series, chapter);
            var entitlementId = KoboEntitlementId.FromChapterId(chapter.Id);

            foreach (var row in group)
            {
                if (!tombstoneKeys.Contains((row.AppUserId, row.ChapterId)))
                {
                    unitOfWork.DataContext.AppUserKoboTombstone.Add(new AppUserKoboTombstone
                    {
                        AppUserId = row.AppUserId,
                        ChapterId = row.ChapterId,
                        EntitlementId = entitlementId,
                        Title = title,
                        CreatedUtc = now,
                    });
                    tombstoneKeys.Add((row.AppUserId, row.ChapterId));
                }
            }
        }

        unitOfWork.DataContext.AppUserKoboSyncedChapter.RemoveRange(syncedRows);
        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>
    /// Synced chapters that are no longer eligible are archived and unsynced so the next
    /// (or current) sync page can emit <c>IsRemoved</c>. Already-archived synced rows are
    /// left alone (removal already delivered).
    /// </summary>
    private async Task ReconcileEligibilityLossAsync(int userId, CancellationToken ct)
    {
        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        var syncedIds = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => s.AppUserId == userId)
            .Where(s => !unitOfWork.DataContext.AppUserKoboArchivedChapter
                .Any(a => a.AppUserId == userId && a.ChapterId == s.ChapterId))
            .Select(s => s.ChapterId)
            .ToListAsync(ct);
        if (syncedIds.Count == 0) return;

        var stillEligible = await EligibleChaptersQuery(libraryIds)
            .Where(c => syncedIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);
        var stillEligibleSet = stillEligible.ToHashSet();
        var lost = syncedIds.Where(id => !stillEligibleSet.Contains(id)).ToList();
        if (lost.Count == 0) return;

        foreach (var chapterId in lost)
        {
            await ArchiveAndUnsyncAsync(userId, chapterId, isDeviceDeleted: false, ct);
        }

        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>
    /// Eligibility archives for chapters that are eligible again are cleared (and unsynced) so
    /// the next sync can re-entitle them. Device-deleted archives are left alone.
    /// </summary>
    private async Task ReconcileEligibilityRestoreAsync(int userId, CancellationToken ct)
    {
        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        var eligibilityArchives = await unitOfWork.DataContext.AppUserKoboArchivedChapter
            .Where(a => a.AppUserId == userId && !a.IsDeviceDeleted)
            .ToListAsync(ct);
        if (eligibilityArchives.Count == 0) return;

        var archivedIds = eligibilityArchives.Select(a => a.ChapterId).ToList();
        var eligibleIds = await EligibleChaptersQuery(libraryIds)
            .Where(c => archivedIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);
        if (eligibleIds.Count == 0) return;

        await ClearArchivesAndUnsyncAsync(userId, eligibleIds, ct);
    }

    private async Task ArchiveAndUnsyncAsync(int userId, int chapterId, bool isDeviceDeleted,
        CancellationToken ct)
    {
        var archived = await unitOfWork.DataContext.AppUserKoboArchivedChapter
            .FirstOrDefaultAsync(a => a.AppUserId == userId && a.ChapterId == chapterId, ct);
        var now = DateTime.UtcNow;
        if (archived == null)
        {
            unitOfWork.DataContext.AppUserKoboArchivedChapter.Add(new AppUserKoboArchivedChapter
            {
                AppUserId = userId,
                ChapterId = chapterId,
                LastModifiedUtc = now,
                IsDeviceDeleted = isDeviceDeleted,
            });
        }
        else
        {
            archived.LastModifiedUtc = now;
            // Device DELETE upgrades an eligibility archive; eligibility loss never clears the flag.
            if (isDeviceDeleted)
            {
                archived.IsDeviceDeleted = true;
            }
        }

        var synced = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => s.AppUserId == userId && s.ChapterId == chapterId)
            .ToListAsync(ct);
        if (synced.Count > 0)
        {
            unitOfWork.DataContext.AppUserKoboSyncedChapter.RemoveRange(synced);
        }
    }

    /// <summary>
    /// Removes the user's archive rows for the given chapters and drops any matching synced-set
    /// rows, then commits. Shared by eligibility-restore reconciliation and device restore.
    /// </summary>
    private async Task ClearArchivesAndUnsyncAsync(int userId, IReadOnlyCollection<int> chapterIds,
        CancellationToken ct)
    {
        if (chapterIds.Count == 0) return;

        var archives = await unitOfWork.DataContext.AppUserKoboArchivedChapter
            .Where(a => a.AppUserId == userId && chapterIds.Contains(a.ChapterId))
            .ToListAsync(ct);
        if (archives.Count > 0)
        {
            unitOfWork.DataContext.AppUserKoboArchivedChapter.RemoveRange(archives);
        }

        var synced = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => s.AppUserId == userId && chapterIds.Contains(s.ChapterId))
            .ToListAsync(ct);
        if (synced.Count > 0)
        {
            unitOfWork.DataContext.AppUserKoboSyncedChapter.RemoveRange(synced);
        }

        await unitOfWork.CommitAsync(ct);
    }
}
