using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Configuration = Kavita.Common.Configuration;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.User;
using Kavita.Services.Kobo;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services;

public class KoboService(
    IUnitOfWork unitOfWork,
    IAuthKeyService authKeyService,
    IEventHub eventHub,
    IMapper mapper,
    IDirectoryService directoryService,
    IDownloadService downloadService)
    : IKoboService
{
    public const string SyncPathPrefix = "api/kobo/";
    public const int SyncItemLimit = 100;
    public const string EpubFormat = "EPUB";
    public const string Epub3Format = "EPUB3";

    private static readonly Guid EmptyGenreId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<string> GetOrCreateSyncUrlAsync(int userId, CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        EnsureSyncUrlAvailable(settings.EnableKoboSync, settings.HostName);

        var authKey = await GetOrCreateKoboAuthKeyAsync(userId, ct);
        return BuildSyncUrl(settings.HostName, settings.BaseUrl, authKey.Key);
    }

    public async Task<string> RotateSyncAuthKeyAsync(int userId, CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        EnsureSyncUrlAvailable(settings.EnableKoboSync, settings.HostName);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.AuthKeys, ct);
        if (user == null) throw new KavitaException("access-denied");

        var authKey = user.AuthKeys.FirstOrDefault(k =>
            string.Equals(k.Name, AuthKeyHelper.KoboKeyName, StringComparison.Ordinal));
        if (authKey == null) throw new KavitaException("kobo-sync-key-missing");

        var oldKeyValue = authKey.Key;
        if (authKey.ExpiresAtUtc != null)
        {
            var originalDuration = authKey.ExpiresAtUtc.Value - authKey.CreatedAtUtc;
            authKey.ExpiresAtUtc = DateTime.UtcNow.Add(originalDuration);
        }

        authKey.Key = AuthKeyHelper.GenerateKey(32);
        await unitOfWork.CommitAsync(ct);
        await authKeyService.InvalidateAsync(oldKeyValue, ct);

        var dto = mapper.Map<AuthKeyDto>(authKey);
        await eventHub.SendMessageToAsync(MessageFactory.AuthKeyUpdate, MessageFactory.AuthKeyUpdatedEvent(dto),
            userId, ct);

        return BuildSyncUrl(settings.HostName, settings.BaseUrl, authKey.Key);
    }

    public async Task RevokeSyncAuthKeyAsync(int userId, CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.AuthKeys, ct);
        if (user == null) throw new KavitaException("access-denied");

        var authKey = user.AuthKeys.FirstOrDefault(k =>
            string.Equals(k.Name, AuthKeyHelper.KoboKeyName, StringComparison.Ordinal));
        if (authKey == null) throw new KavitaException("kobo-sync-key-missing");

        var oldKeyValue = authKey.Key;
        var authKeyId = authKey.Id;
        unitOfWork.UserRepository.Delete(authKey);
        await unitOfWork.CommitAsync(ct);

        await authKeyService.InvalidateAsync(oldKeyValue, ct);
        await eventHub.SendMessageToAsync(MessageFactory.AuthKeyDeleted, MessageFactory.AuthKeyDeletedEvent(authKeyId),
            userId, ct);
    }

    public async Task<int> ResolveUserIdAsync(string authToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(authToken))
        {
            throw new KavitaUnauthenticatedUserException("kobo-auth-invalid");
        }

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.EnableKoboSync)
        {
            throw new KavitaUnauthenticatedUserException("kobo-sync-disabled");
        }

        var userId = await unitOfWork.DataContext.AppUserAuthKey
            .Where(k => k.Key == authToken && k.Name == AuthKeyHelper.KoboKeyName)
            .HasNotExpired()
            .Select(k => k.AppUserId)
            .FirstOrDefaultAsync(ct);

        if (userId <= 0)
        {
            throw new KavitaUnauthenticatedUserException("kobo-auth-invalid");
        }

        return userId;
    }

    public async Task<KoboInitializationResult> GetInitializationAsync(string authToken,
        CancellationToken ct = default)
    {
        await ResolveUserIdAsync(authToken, ct);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.HostName))
        {
            throw new KavitaException("kobo-hostname-required");
        }

        var publicBase = BuildPublicBase(settings.HostName, settings.BaseUrl);
        var tokenBase = $"{publicBase}/{SyncPathPrefix}{authToken}";

        var resources = NativeKoboResources.CreateCopy();
        resources["image_host"] = publicBase;
        resources["image_url_template"] =
            $"{tokenBase}/{{ImageId}}/{{width}}/{{height}}/false/image.jpg";
        resources["image_url_quality_template"] =
            $"{tokenBase}/{{ImageId}}/{{width}}/{{height}}/{{Quality}}/{{IsGreyscale}}/image.jpg";
        resources["library_sync"] = $"{tokenBase}/v1/library/sync";

        return new KoboInitializationResult { Resources = resources };
    }

    public async Task<KoboAuthTokenDto> CreateDeviceAuthResponseAsync(string authToken, string? userKey,
        CancellationToken ct = default)
    {
        await ResolveUserIdAsync(authToken, ct);

        return new KoboAuthTokenDto
        {
            AccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
            RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
            TokenType = "Bearer",
            TrackingId = Guid.NewGuid().ToString(),
            UserKey = userKey ?? string.Empty,
        };
    }

    public async Task<KoboLibrarySyncResult> SyncLibraryAsync(string authToken, string? syncTokenHeader,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.HostName))
        {
            throw new KavitaException("kobo-hostname-required");
        }

        await ReconcileEligibilityLossAsync(userId, ct);

        var syncToken = KoboSyncToken.FromHeader(syncTokenHeader);
        var hasSyncedRows = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .AnyAsync(s => s.AppUserId == userId, ct);
        if (!hasSyncedRows)
        {
            syncToken.BooksLastCreated = DateTime.MinValue;
            syncToken.BooksLastModified = DateTime.MinValue;
            syncToken.ReadingStateLastModified = DateTime.MinValue;
        }

        var publicBase = BuildPublicBase(settings.HostName, settings.BaseUrl);
        var tokenBase = $"{publicBase}/{SyncPathPrefix}{authToken}";

        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        var items = new JsonArray();
        var newBooksLastModified = syncToken.BooksLastModified;
        var newBooksLastCreated = syncToken.BooksLastCreated;
        var newArchiveLastModified = syncToken.ArchiveLastModified;
        var remainingSlots = SyncItemLimit;

        // Removals first: archived (not in synced-set) + hard-delete tombstones.
        var removalSlots = await AppendArchiveRemovalsAsync(userId, tokenBase, syncToken, items,
            remainingSlots, ct);
        remainingSlots -= removalSlots.Emitted;
        if (removalSlots.MaxArchiveModified > newArchiveLastModified)
        {
            newArchiveLastModified = removalSlots.MaxArchiveModified;
        }

        if (remainingSlots > 0)
        {
            var tombstoneSlots = await AppendTombstoneRemovalsAsync(userId, items, remainingSlots, ct);
            remainingSlots -= tombstoneSlots;
        }

        if (remainingSlots > 0)
        {
            var unsyncedQuery = EligibleChaptersQuery(libraryIds)
                .Where(c => !unitOfWork.DataContext.AppUserKoboArchivedChapter
                    .Any(a => a.AppUserId == userId && a.ChapterId == c.Id))
                .Where(c => !unitOfWork.DataContext.AppUserKoboSyncedChapter
                    .Any(s => s.AppUserId == userId && s.ChapterId == c.Id))
                .OrderBy(c => c.LastModifiedUtc)
                .ThenBy(c => c.Id);

            var page = await unsyncedQuery
                .Take(remainingSlots)
                .Include(c => c.Files)
                .Include(c => c.People).ThenInclude(p => p.Person)
                .Include(c => c.Volume).ThenInclude(v => v.Series).ThenInclude(s => s.Metadata)
                .ThenInclude(m => m!.People).ThenInclude(p => p.Person)
                .AsSplitQuery()
                .ToListAsync(ct);

            foreach (var chapter in page)
            {
                var series = chapter.Volume.Series;
                var entitlementUuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
                var entitlement = BuildEntitlementPayload(chapter, series, entitlementUuid, tokenBase,
                    isRemoved: false);

                var created = chapter.CreatedUtc == default ? chapter.Created : chapter.CreatedUtc;
                var isNew = created > syncToken.BooksLastCreated;
                items.Add(new JsonObject
                {
                    [isNew ? "NewEntitlement" : "ChangedEntitlement"] = entitlement,
                });

                var modified = chapter.LastModifiedUtc == default ? chapter.LastModified : chapter.LastModifiedUtc;
                if (modified > newBooksLastModified) newBooksLastModified = modified;
                if (created > newBooksLastCreated) newBooksLastCreated = created;

                unitOfWork.DataContext.AppUserKoboSyncedChapter.Add(new AppUserKoboSyncedChapter
                {
                    AppUserId = userId,
                    ChapterId = chapter.Id,
                });
            }

            remainingSlots -= page.Count;
        }

        if (items.Count > 0)
        {
            await unitOfWork.CommitAsync(ct);
        }

        var remainingRemovals = await CountPendingRemovalsAsync(userId, ct);
        var remainingNew = await EligibleChaptersQuery(libraryIds)
            .Where(c => !unitOfWork.DataContext.AppUserKoboArchivedChapter
                .Any(a => a.AppUserId == userId && a.ChapterId == c.Id))
            .Where(c => !unitOfWork.DataContext.AppUserKoboSyncedChapter
                .Any(s => s.AppUserId == userId && s.ChapterId == c.Id))
            .CountAsync(ct);
        var contSync = remainingRemovals + remainingNew > 0;

        // Hold books_last_created until the final page so New vs Changed stays stable across continue pages.
        if (!contSync)
        {
            syncToken.BooksLastCreated = newBooksLastCreated;
        }

        syncToken.BooksLastModified = newBooksLastModified;
        syncToken.ArchiveLastModified = newArchiveLastModified;

        return new KoboLibrarySyncResult
        {
            Items = items,
            SyncToken = syncToken.ToHeaderValue(),
            Continue = contSync,
        };
    }

    public async Task DeleteEntitlementAsync(string authToken, string entitlementId,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        if (!Guid.TryParse(entitlementId, out var entitlementGuid))
        {
            return;
        }

        var chapterId = await ResolveChapterIdByEntitlementAsync(entitlementGuid, ct);
        if (chapterId <= 0)
        {
            // Already gone (or unknown): treat as success so the device can finish cleanup.
            return;
        }

        await ArchiveAndUnsyncAsync(userId, chapterId, ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task ForceFullSyncAsync(int userId, CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.None, ct);
        if (user == null) throw new KavitaException("access-denied");

        var synced = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => s.AppUserId == userId)
            .ToListAsync(ct);
        if (synced.Count == 0) return;

        unitOfWork.DataContext.AppUserKoboSyncedChapter.RemoveRange(synced);
        await unitOfWork.CommitAsync(ct);
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
            var title = BuildTitle(chapter.Volume.Series, chapter);
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

    public async Task<IReadOnlyList<JsonObject>> GetMetadataAsync(string authToken, string entitlementId,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.HostName))
        {
            throw new KavitaException("kobo-hostname-required");
        }

        var chapter = await ResolveEligibleChapterAsync(userId, entitlementId, ct);
        if (chapter == null) throw new KavitaException("kobo-entitlement-not-found");

        var publicBase = BuildPublicBase(settings.HostName, settings.BaseUrl);
        var tokenBase = $"{publicBase}/{SyncPathPrefix}{authToken}";
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
        var metadata = BuildBookMetadata(chapter, chapter.Volume.Series, uuid, tokenBase);
        return [metadata];
    }

    public async Task<KoboDownloadResult> GetDownloadAsync(string authToken, string entitlementId, string format,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        if (!IsSupportedDownloadFormat(format))
        {
            throw new KavitaException("kobo-format-unsupported");
        }

        var chapter = await ResolveEligibleChapterAsync(userId, entitlementId, ct);
        if (chapter == null) throw new KavitaException("kobo-entitlement-not-found");

        var epub = PreferNativeEpub(chapter.Files);
        if (epub == null) throw new KavitaException("kobo-epub-missing");

        return new KoboDownloadResult
        {
            FilePath = epub.FilePath,
            ContentType = downloadService.GetContentTypeFromFile(epub.FilePath),
            FileDownloadName = Path.GetFileName(epub.FilePath),
        };
    }

    public async Task<KoboCoverResult?> GetCoverAsync(string authToken, string entitlementId,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        var chapter = await ResolveAccessibleChapterAsync(userId, entitlementId, requireEpub: false, ct);
        if (chapter == null) return null;

        var coverFile = ResolveCoverFileName(chapter);
        if (string.IsNullOrEmpty(coverFile)) return null;

        var path = Path.Join(directoryService.CoverImageDirectory, coverFile);
        if (!File.Exists(path)) return null;

        return new KoboCoverResult { FilePath = path };
    }

    public object GetEmptyStub() => new JsonObject();

    public object GetLoyaltyBenefitsStub() => new JsonObject
    {
        ["Benefits"] = new JsonObject(),
    };

    public object GetAnalyticsTestsStub(string? koboUserKey) => new JsonObject
    {
        ["Result"] = "Success",
        ["TestKey"] = koboUserKey ?? string.Empty,
        ["Tests"] = new JsonObject(),
    };

    public object GetReadingStateStub(string entitlementId)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return new JsonArray
        {
            new JsonObject
            {
                ["EntitlementId"] = entitlementId,
                ["Created"] = now,
                ["LastModified"] = now,
                ["PriorityTimestamp"] = now,
                ["StatusInfo"] = new JsonObject
                {
                    ["LastModified"] = now,
                    ["Status"] = "ReadyToRead",
                    ["TimesStartedReading"] = 0,
                },
                ["Statistics"] = new JsonObject
                {
                    ["LastModified"] = now,
                },
                ["CurrentBookmark"] = new JsonObject
                {
                    ["LastModified"] = now,
                },
            },
        };
    }

    public object PutReadingStateStub(string entitlementId)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return new JsonObject
        {
            ["RequestResult"] = "Success",
            ["UpdateResults"] = new JsonArray
            {
                new JsonObject
                {
                    ["EntitlementId"] = entitlementId,
                    ["LastModified"] = now,
                    ["PriorityTimestamp"] = now,
                    ["CurrentBookmarkResult"] = new JsonObject { ["Result"] = "Success" },
                    ["StatisticsResult"] = new JsonObject { ["Result"] = "Success" },
                    ["StatusInfoResult"] = new JsonObject { ["Result"] = "Success" },
                },
            },
        };
    }

    private async Task<AppUserAuthKey> GetOrCreateKoboAuthKeyAsync(int userId, CancellationToken ct)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.AuthKeys, ct);
        if (user == null) throw new KavitaException("access-denied");

        var existing = user.AuthKeys.FirstOrDefault(k =>
            string.Equals(k.Name, AuthKeyHelper.KoboKeyName, StringComparison.Ordinal));
        if (existing != null) return existing;

        var newKey = new AppUserAuthKey
        {
            Name = AuthKeyHelper.KoboKeyName,
            Key = AuthKeyHelper.GenerateKey(32),
            AppUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            Provider = AuthKeyProvider.User,
        };
        unitOfWork.UserRepository.Add(newKey);
        await unitOfWork.CommitAsync(ct);

        var dto = mapper.Map<AuthKeyDto>(newKey);
        await eventHub.SendMessageToAsync(MessageFactory.AuthKeyUpdate, MessageFactory.AuthKeyUpdatedEvent(dto),
            userId, ct);

        return newKey;
    }

    private async Task<List<int>> GetAllowedLibraryIdsAsync(int userId, CancellationToken ct)
    {
        return await unitOfWork.DataContext.Library
            .Where(l => l.AllowKoboSync && l.AppUsers.Any(u => u.Id == userId))
            .Select(l => l.Id)
            .ToListAsync(ct);
    }

    private IQueryable<Chapter> EligibleChaptersQuery(IReadOnlyCollection<int> libraryIds)
    {
        return unitOfWork.DataContext.Chapter
            .Where(c => libraryIds.Contains(c.Volume.Series.LibraryId)
                        && c.Files.Any(f => f.Format == MangaFormat.Epub));
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
            await ArchiveAndUnsyncAsync(userId, chapterId, ct);
        }

        await unitOfWork.CommitAsync(ct);
    }

    private async Task ArchiveAndUnsyncAsync(int userId, int chapterId, CancellationToken ct)
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
            });
        }
        else
        {
            archived.LastModifiedUtc = now;
        }

        var synced = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => s.AppUserId == userId && s.ChapterId == chapterId)
            .ToListAsync(ct);
        if (synced.Count > 0)
        {
            unitOfWork.DataContext.AppUserKoboSyncedChapter.RemoveRange(synced);
        }
    }

    private async Task<(int Emitted, DateTime MaxArchiveModified)> AppendArchiveRemovalsAsync(
        int userId, string tokenBase, KoboSyncToken syncToken, JsonArray items, int limit,
        CancellationToken ct)
    {
        if (limit <= 0) return (0, DateTime.MinValue);

        // Pending archive removals are not constrained to current library allow-list —
        // eligibility loss may have already revoked access.
        var pending = await unitOfWork.DataContext.AppUserKoboArchivedChapter
            .Where(a => a.AppUserId == userId)
            .Where(a => !unitOfWork.DataContext.AppUserKoboSyncedChapter
                .Any(s => s.AppUserId == userId && s.ChapterId == a.ChapterId))
            .OrderBy(a => a.LastModifiedUtc)
            .ThenBy(a => a.ChapterId)
            .Take(limit)
            .ToListAsync(ct);

        if (pending.Count == 0) return (0, DateTime.MinValue);

        var chapterIds = pending.Select(a => a.ChapterId).ToList();
        var chapters = await unitOfWork.DataContext.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .Include(c => c.Files)
            .Include(c => c.People).ThenInclude(p => p.Person)
            .Include(c => c.Volume).ThenInclude(v => v.Series).ThenInclude(s => s.Metadata)
            .ThenInclude(m => m!.People).ThenInclude(p => p.Person)
            .AsSplitQuery()
            .ToListAsync(ct);
        var chapterById = chapters.ToDictionary(c => c.Id);

        var maxArchive = DateTime.MinValue;
        var emitted = 0;
        foreach (var archive in pending)
        {
            if (!chapterById.TryGetValue(archive.ChapterId, out var chapter))
            {
                // Chapter vanished without a tombstone — drop orphan archive row.
                unitOfWork.DataContext.AppUserKoboArchivedChapter.Remove(archive);
                continue;
            }

            var series = chapter.Volume.Series;
            var entitlementUuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
            var entitlement = BuildEntitlementPayload(chapter, series, entitlementUuid, tokenBase,
                isRemoved: true);

            var created = chapter.CreatedUtc == default ? chapter.Created : chapter.CreatedUtc;
            var isNew = created > syncToken.BooksLastCreated;
            items.Add(new JsonObject
            {
                [isNew ? "NewEntitlement" : "ChangedEntitlement"] = entitlement,
            });

            unitOfWork.DataContext.AppUserKoboSyncedChapter.Add(new AppUserKoboSyncedChapter
            {
                AppUserId = userId,
                ChapterId = chapter.Id,
            });

            if (archive.LastModifiedUtc > maxArchive) maxArchive = archive.LastModifiedUtc;
            emitted++;
        }

        return (emitted, maxArchive);
    }

    private async Task<int> AppendTombstoneRemovalsAsync(int userId, JsonArray items, int limit,
        CancellationToken ct)
    {
        if (limit <= 0) return 0;

        var tombstones = await unitOfWork.DataContext.AppUserKoboTombstone
            .Where(t => t.AppUserId == userId)
            .OrderBy(t => t.CreatedUtc)
            .ThenBy(t => t.ChapterId)
            .Take(limit)
            .ToListAsync(ct);

        foreach (var tombstone in tombstones)
        {
            var entitlementUuid = tombstone.EntitlementId.ToString();
            var entitlement = BuildTombstoneEntitlementPayload(tombstone, entitlementUuid);
            items.Add(new JsonObject
            {
                ["ChangedEntitlement"] = entitlement,
            });
            unitOfWork.DataContext.AppUserKoboTombstone.Remove(tombstone);
        }

        return tombstones.Count;
    }

    private async Task<int> CountPendingRemovalsAsync(int userId, CancellationToken ct)
    {
        var archiveCount = await unitOfWork.DataContext.AppUserKoboArchivedChapter
            .Where(a => a.AppUserId == userId)
            .Where(a => !unitOfWork.DataContext.AppUserKoboSyncedChapter
                .Any(s => s.AppUserId == userId && s.ChapterId == a.ChapterId))
            .CountAsync(ct);
        var tombstoneCount = await unitOfWork.DataContext.AppUserKoboTombstone
            .CountAsync(t => t.AppUserId == userId, ct);
        return archiveCount + tombstoneCount;
    }

    private async Task<int> ResolveChapterIdByEntitlementAsync(Guid entitlementGuid, CancellationToken ct)
    {
        // Prefer exact match via known synced/archived rows, then scan chapter ids.
        var knownIds = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Select(s => s.ChapterId)
            .Union(unitOfWork.DataContext.AppUserKoboArchivedChapter.Select(a => a.ChapterId))
            .Distinct()
            .ToListAsync(ct);
        var fromKnown = knownIds.FirstOrDefault(id => KoboEntitlementId.FromChapterId(id) == entitlementGuid);
        if (fromKnown > 0) return fromKnown;

        var allIds = await unitOfWork.DataContext.Chapter.Select(c => c.Id).ToListAsync(ct);
        return allIds.FirstOrDefault(id => KoboEntitlementId.FromChapterId(id) == entitlementGuid);
    }

    private async Task<Chapter?> ResolveEligibleChapterAsync(int userId, string entitlementId,
        CancellationToken ct) =>
        await ResolveAccessibleChapterAsync(userId, entitlementId, requireEpub: true, ct);

    private async Task<Chapter?> ResolveAccessibleChapterAsync(int userId, string entitlementId,
        bool requireEpub, CancellationToken ct)
    {
        if (!Guid.TryParse(entitlementId, out var entitlementGuid)) return null;

        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        if (libraryIds.Count == 0) return null;

        var query = unitOfWork.DataContext.Chapter
            .Where(c => libraryIds.Contains(c.Volume.Series.LibraryId));
        if (requireEpub)
        {
            query = query.Where(c => c.Files.Any(f => f.Format == MangaFormat.Epub));
        }

        var candidates = await query
            .Select(c => c.Id)
            .ToListAsync(ct);

        var chapterId = candidates.FirstOrDefault(id =>
            KoboEntitlementId.FromChapterId(id) == entitlementGuid);
        if (chapterId <= 0) return null;

        return await unitOfWork.DataContext.Chapter
            .Include(c => c.Files)
            .Include(c => c.People).ThenInclude(p => p.Person)
            .Include(c => c.Volume).ThenInclude(v => v.Series).ThenInclude(s => s.Metadata)
            .ThenInclude(m => m!.People).ThenInclude(p => p.Person)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == chapterId, ct);
    }

    private JsonObject BuildTombstoneEntitlementPayload(AppUserKoboTombstone tombstone,
        string entitlementUuid)
    {
        var now = FormatKoboTimestamp(DateTime.UtcNow);
        var created = FormatKoboTimestamp(tombstone.CreatedUtc);
        return new JsonObject
        {
            ["BookEntitlement"] = new JsonObject
            {
                ["Accessibility"] = "Full",
                ["ActivePeriod"] = new JsonObject { ["From"] = now },
                ["Created"] = created,
                ["CrossRevisionId"] = entitlementUuid,
                ["Id"] = entitlementUuid,
                ["IsRemoved"] = true,
                ["IsHiddenFromArchive"] = false,
                ["IsLocked"] = false,
                ["LastModified"] = now,
                ["OriginCategory"] = "Imported",
                ["RevisionId"] = entitlementUuid,
                ["Status"] = "Active",
            },
            ["BookMetadata"] = new JsonObject
            {
                ["Categories"] = new JsonArray { EmptyGenreId.ToString() },
                ["CoverImageId"] = entitlementUuid,
                ["CrossRevisionId"] = entitlementUuid,
                ["CurrentDisplayPrice"] = new JsonObject
                {
                    ["CurrencyCode"] = "USD",
                    ["TotalAmount"] = 0,
                },
                ["CurrentLoveDisplayPrice"] = new JsonObject { ["TotalAmount"] = 0 },
                ["Description"] = null,
                ["DownloadUrls"] = new JsonArray(),
                ["EntitlementId"] = entitlementUuid,
                ["ExternalIds"] = new JsonArray(),
                ["Genre"] = EmptyGenreId.ToString(),
                ["IsEligibleForKoboLove"] = false,
                ["IsInternetArchive"] = false,
                ["IsPreOrder"] = false,
                ["IsSocialEnabled"] = true,
                ["Language"] = "en",
                ["PhoneticPronunciations"] = new JsonObject(),
                ["Publisher"] = new JsonObject
                {
                    ["Imprint"] = string.Empty,
                    ["Name"] = null,
                },
                ["RevisionId"] = entitlementUuid,
                ["Title"] = tombstone.Title,
                ["WorkId"] = entitlementUuid,
                ["Contributors"] = null,
            },
        };
    }

    private JsonObject BuildEntitlementPayload(Chapter chapter, Series series, string entitlementUuid,
        string tokenBase, bool isRemoved)
    {
        return new JsonObject
        {
            ["BookEntitlement"] = BuildBookEntitlement(chapter, entitlementUuid, isRemoved),
            ["BookMetadata"] = BuildBookMetadata(chapter, series, entitlementUuid, tokenBase),
        };
    }

    private static JsonObject BuildBookEntitlement(Chapter chapter, string entitlementUuid, bool isRemoved)
    {
        var created = FormatKoboTimestamp(chapter.CreatedUtc == default ? chapter.Created : chapter.CreatedUtc);
        var modified = FormatKoboTimestamp(
            chapter.LastModifiedUtc == default ? chapter.LastModified : chapter.LastModifiedUtc);

        return new JsonObject
        {
            ["Accessibility"] = "Full",
            ["ActivePeriod"] = new JsonObject { ["From"] = FormatKoboTimestamp(DateTime.UtcNow) },
            ["Created"] = created,
            ["CrossRevisionId"] = entitlementUuid,
            ["Id"] = entitlementUuid,
            ["IsRemoved"] = isRemoved,
            ["IsHiddenFromArchive"] = false,
            ["IsLocked"] = false,
            ["LastModified"] = modified,
            ["OriginCategory"] = "Imported",
            ["RevisionId"] = entitlementUuid,
            ["Status"] = "Active",
        };
    }

    private JsonObject BuildBookMetadata(Chapter chapter, Series series, string entitlementUuid, string tokenBase)
    {
        var epub = PreferNativeEpub(chapter.Files);
        var downloadUrls = new JsonArray();
        if (epub != null)
        {
            var size = epub.Bytes > 0 ? epub.Bytes : 0;
            var url = $"{tokenBase}/download/{entitlementUuid}/epub";
            // Advertise both so firmware that prefers EPUB3 still resolves a download.
            downloadUrls.Add(BuildDownloadUrl(Epub3Format, size, url));
            downloadUrls.Add(BuildDownloadUrl(EpubFormat, size, url));
        }

        var writers = ResolveWriters(chapter, series.Metadata);
        var publisher = ResolvePublisher(chapter, series.Metadata);
        var description = !string.IsNullOrWhiteSpace(chapter.Summary)
            ? chapter.Summary
            : series.Metadata?.Summary;
        var language = !string.IsNullOrWhiteSpace(series.Metadata?.Language)
            ? series.Metadata!.Language
            : "en";

        var metadata = new JsonObject
        {
            ["Categories"] = new JsonArray { EmptyGenreId.ToString() },
            ["CoverImageId"] = entitlementUuid,
            ["CrossRevisionId"] = entitlementUuid,
            ["CurrentDisplayPrice"] = new JsonObject
            {
                ["CurrencyCode"] = "USD",
                ["TotalAmount"] = 0,
            },
            ["CurrentLoveDisplayPrice"] = new JsonObject { ["TotalAmount"] = 0 },
            ["Description"] = description,
            ["DownloadUrls"] = downloadUrls,
            ["EntitlementId"] = entitlementUuid,
            ["ExternalIds"] = new JsonArray(),
            ["Genre"] = EmptyGenreId.ToString(),
            ["IsEligibleForKoboLove"] = false,
            ["IsInternetArchive"] = false,
            ["IsPreOrder"] = false,
            ["IsSocialEnabled"] = true,
            ["Language"] = language,
            ["PhoneticPronunciations"] = new JsonObject(),
            ["Publisher"] = new JsonObject
            {
                ["Imprint"] = string.Empty,
                ["Name"] = publisher,
            },
            ["RevisionId"] = entitlementUuid,
            ["Title"] = BuildTitle(series, chapter),
            ["WorkId"] = entitlementUuid,
            ["Series"] = BuildSeriesMetadata(series, chapter),
        };

        if (chapter.ReleaseDate != default)
        {
            metadata["PublicationDate"] = FormatKoboTimestamp(chapter.ReleaseDate);
        }

        if (writers.Count > 0)
        {
            var roles = new JsonArray();
            var names = new JsonArray();
            foreach (var writer in writers)
            {
                roles.Add(new JsonObject { ["Name"] = writer });
                names.Add(writer);
            }

            metadata["ContributorRoles"] = roles;
            metadata["Contributors"] = names;
        }
        else
        {
            metadata["Contributors"] = null;
        }

        return metadata;
    }

    private static JsonObject BuildDownloadUrl(string format, long size, string url) => new()
    {
        ["Format"] = format,
        ["Size"] = size,
        ["Url"] = url,
        ["Platform"] = "Generic",
    };

    private static JsonObject BuildSeriesMetadata(Series series, Chapter chapter)
    {
        var seriesMeta = new JsonObject
        {
            ["Name"] = series.Name,
            ["Id"] = KoboEntitlementId.CreateVersion5(KoboEntitlementId.Namespace, $"series:{series.Name}")
                .ToString(),
        };

        // Omit placeholder/default chapter numbers so Kobo does not sort specials as -100000.
        if (chapter.MinNumber.IsNot(Parser.DefaultChapterNumber) && chapter.MinNumber > 0)
        {
            seriesMeta["Number"] = chapter.MinNumber;
            seriesMeta["NumberFloat"] = chapter.MinNumber;
        }

        return seriesMeta;
    }

    private static bool IsSupportedDownloadFormat(string format) =>
        string.Equals(format, "epub", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, EpubFormat, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, "epub3", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, Epub3Format, StringComparison.OrdinalIgnoreCase);

    internal static string BuildTitle(Series series, Chapter chapter)
    {
        string chapterLabel;
        if (chapter.IsSpecial)
        {
            var special = !string.IsNullOrWhiteSpace(chapter.TitleName)
                ? chapter.TitleName
                : Parser.CleanSpecialTitle(chapter.Title);
            chapterLabel = string.IsNullOrWhiteSpace(special) ? chapter.Range : special;
        }
        else if (!string.IsNullOrWhiteSpace(chapter.TitleName))
        {
            chapterLabel = chapter.TitleName;
        }
        else if (!string.IsNullOrWhiteSpace(chapter.Title))
        {
            chapterLabel = chapter.Title;
        }
        else
        {
            chapterLabel = chapter.Range;
        }

        return $"{series.Name} - {chapterLabel}";
    }

    internal static MangaFile? PreferNativeEpub(IEnumerable<MangaFile> files) =>
        files.FirstOrDefault(f => f.Format == MangaFormat.Epub);

    private static List<string> ResolveWriters(Chapter chapter, SeriesMetadata? metadata)
    {
        var chapterWriters = chapter.People
            .Where(p => p.Role == PersonRole.Writer)
            .Select(p => p.Person.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        if (chapterWriters.Count > 0) return chapterWriters;

        return metadata?.People
            .Where(p => p.Role == PersonRole.Writer)
            .Select(p => p.Person.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList() ?? [];
    }

    private static string? ResolvePublisher(Chapter chapter, SeriesMetadata? metadata)
    {
        var chapterPublisher = chapter.People
            .FirstOrDefault(p => p.Role == PersonRole.Publisher)?.Person.Name;
        if (!string.IsNullOrWhiteSpace(chapterPublisher)) return chapterPublisher;

        return metadata?.People
            .FirstOrDefault(p => p.Role == PersonRole.Publisher)?.Person.Name;
    }

    private static string? ResolveCoverFileName(Chapter chapter)
    {
        if (!string.IsNullOrEmpty(chapter.CoverImage)) return chapter.CoverImage;
        if (!string.IsNullOrEmpty(chapter.Volume?.CoverImage)) return chapter.Volume.CoverImage;
        return chapter.Volume?.Series?.CoverImage;
    }

    private static string FormatKoboTimestamp(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        return utc.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    private static void EnsureSyncUrlAvailable(bool enableKoboSync, string? hostName)
    {
        if (!enableKoboSync) throw new KavitaException("kobo-sync-disabled");
        if (string.IsNullOrWhiteSpace(hostName)) throw new KavitaException("kobo-hostname-required");
    }

    /// <summary>
    /// Compose public sync URL from configured HostName + BaseUrl only (no request-host fallback).
    /// </summary>
    public static string BuildSyncUrl(string hostName, string baseUrl, string authKey)
    {
        return $"{BuildPublicBase(hostName, baseUrl)}/{SyncPathPrefix}{authKey}";
    }

    /// <summary>
    /// Public origin + optional BaseUrl path prefix (no trailing slash).
    /// </summary>
    public static string BuildPublicBase(string hostName, string baseUrl)
    {
        var origin = UrlHelper.RemoveEndingSlash(hostName.Trim());
        var pathBase = string.Empty;
        if (!string.IsNullOrEmpty(baseUrl) && !baseUrl.Equals(Configuration.DefaultBaseUrl))
        {
            pathBase = baseUrl;
            if (pathBase.EndsWith('/'))
            {
                pathBase = pathBase[..^1];
            }

            if (!pathBase.StartsWith('/'))
            {
                pathBase = "/" + pathBase;
            }
        }

        return $"{origin}{pathBase}";
    }
}
