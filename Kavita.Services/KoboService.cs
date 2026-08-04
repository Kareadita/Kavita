using System;
using System.Collections.Concurrent;
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
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services.Kobo;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services;

public partial class KoboService(
    IUnitOfWork unitOfWork,
    IAuthKeyService authKeyService,
    IEventHub eventHub,
    IMapper mapper,
    IDirectoryService directoryService,
    IDownloadService downloadService,
    IKoboConversionService koboConversionService,
    IKoboLocationMapper koboLocationMapper,
    IKoboConvertProgressLocationService koboConvertProgressLocation)
    : IKoboService
{
    public const string SyncPathPrefix = "api/kobo/";
    public const int DefaultSyncPageSize = KoboSettingsDefaults.SyncPageSize;
    public const int MinSyncPageSize = KoboSettingsDefaults.MinSyncPageSize;
    public const int MaxSyncPageSize = KoboSettingsDefaults.MaxSyncPageSize;
    public const int SyncLockWaitSeconds = 30;
    public const string SyncBusyMessage = "kobo-sync-busy";
    public const string EntitlementNotFoundMessage = "kobo-entitlement-not-found";
    public const string EpubMissingMessage = "kobo-epub-missing";
    public const string FormatUnsupportedMessage = "kobo-format-unsupported";
    public const string ReadingStateMalformedMessage = "kobo-reading-state-malformed";
    public const string EpubFormat = "EPUB";
    public const string Epub3Format = "EPUB3";
    public const string KepubFormat = "KEPUB";
    public const string KepubDownloadFileExtension = ".kepub.epub";

    /// <summary>Per-user mutex for sync-state mutations (library/sync, entitlements, reading state, tags; process-wide).</summary>
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> LibrarySyncLocks = new();

    /// <summary>Test seam: override lock wait. Reset via <see cref="ResetSyncLockForTests"/>.</summary>
    internal static TimeSpan? SyncLockWaitOverride { get; set; }

    internal static void ResetSyncLockForTests() => SyncLockWaitOverride = null;

    /// <summary>Test seam: hold the per-user sync lock (caller must dispose).</summary>
    internal static Task<IDisposable> HoldLibrarySyncLockForTestsAsync(int userId,
        CancellationToken ct = default) =>
        AcquireLibrarySyncLockAsync(userId, ct);

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
        // Device Tag mutations must hit Kavita (no store proxy in v2).
        resources["tags"] = $"{tokenBase}/v1/library/tags";
        resources["tag_items"] = $"{tokenBase}/v1/library/tags/{{TagId}}/Items";
        resources["delete_tag"] = $"{tokenBase}/v1/library/tags/{{TagId}}";
        resources["delete_tag_items"] = $"{tokenBase}/v1/library/tags/{{TagId}}/items/delete";
        resources["rename_tag"] = $"{tokenBase}/v1/library/tags/{{TagId}}";

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
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);
        return await SyncLibraryCoreAsync(userId, authToken, syncTokenHeader, ct);
    }

    private async Task<KoboLibrarySyncResult> SyncLibraryCoreAsync(int userId, string authToken,
        string? syncTokenHeader, CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.HostName))
        {
            throw new KavitaException("kobo-hostname-required");
        }

        await ReconcileEligibilityRestoreAsync(userId, ct);
        await ReconcileEligibilityLossAsync(userId, ct);

        var syncToken = KoboSyncToken.FromHeader(syncTokenHeader);
        await ApplyForcedFullSyncResetAsync(userId, syncToken, ct);

        var tokenBase = BuildTokenBase(settings, authToken);
        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        var state = new SyncPageState(syncToken, ResolvePageSize(settings));

        // Removals first: archived (not in synced-set) + hard-delete tombstones.
        await AppendRemovalsAsync(userId, tokenBase, syncToken, settings, state, ct);
        await EmitNewEntitlementsAsync(userId, libraryIds, tokenBase, syncToken, settings, state, ct);
        await AppendChangedReadingStatesToPageAsync(userId, syncToken, state, ct);

        // Tag deltas are not limited by page size — append all matching shelves every page.
        var tagWatermark = await AppendTagDeltasAsync(userId, libraryIds, state.Items,
            syncToken.TagsLastModified, ct);
        if (tagWatermark > state.NewTagsLastModified) state.NewTagsLastModified = tagWatermark;

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync(ct);
        }

        return await FinalizeSyncTokenAsync(userId, libraryIds, syncToken, state, ct);
    }

    /// <summary>
    /// A cleared synced-set (force full sync) resets book/reading-state/tags watermarks so the next
    /// page re-emits everything from scratch.
    /// </summary>
    private async Task ApplyForcedFullSyncResetAsync(int userId, KoboSyncToken syncToken, CancellationToken ct)
    {
        var hasSyncedRows = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .AnyAsync(s => s.AppUserId == userId, ct);
        if (hasSyncedRows) return;

        syncToken.BooksLastCreated = DateTime.MinValue;
        syncToken.BooksLastModified = DateTime.MinValue;
        syncToken.ReadingStateLastModified = DateTime.MinValue;
        // Force full sync clears the synced-set; reset tags watermark so shelves re-emit.
        syncToken.TagsLastModified = DateTime.MinValue;
    }

    private static int ResolvePageSize(ServerSettingDto settings) =>
        settings.KoboSyncPageSize > 0
            ? Math.Clamp(settings.KoboSyncPageSize, MinSyncPageSize, MaxSyncPageSize)
            : DefaultSyncPageSize;

    private static string BuildTokenBase(ServerSettingDto settings, string authToken) =>
        $"{BuildPublicBase(settings.HostName, settings.BaseUrl)}/{SyncPathPrefix}{authToken}";

    /// <summary>Eligible chapters that are neither archived nor already in this user's synced-set.</summary>
    private IQueryable<Chapter> UnsyncedEligibleChaptersQuery(int userId, IReadOnlyCollection<int> libraryIds) =>
        EligibleChaptersQuery(libraryIds)
            .Where(c => !unitOfWork.DataContext.AppUserKoboArchivedChapter
                .Any(a => a.AppUserId == userId && a.ChapterId == c.Id))
            .Where(c => !unitOfWork.DataContext.AppUserKoboSyncedChapter
                .Any(s => s.AppUserId == userId && s.ChapterId == c.Id));

    private async Task AppendRemovalsAsync(int userId, string tokenBase, KoboSyncToken syncToken,
        ServerSettingDto settings, SyncPageState state, CancellationToken ct)
    {
        var removalSlots = await AppendArchiveRemovalsAsync(userId, tokenBase, syncToken, state.Items,
            state.RemainingSlots, settings.EnableKepubConversion, ct);
        state.RemainingSlots -= removalSlots.Emitted;
        if (removalSlots.MaxArchiveModified > state.NewArchiveLastModified)
        {
            state.NewArchiveLastModified = removalSlots.MaxArchiveModified;
        }

        if (state.RemainingSlots > 0)
        {
            var tombstoneSlots = await AppendTombstoneRemovalsAsync(userId, state.Items, state.RemainingSlots, ct);
            state.RemainingSlots -= tombstoneSlots;
        }
    }

    private async Task EmitNewEntitlementsAsync(int userId, IReadOnlyCollection<int> libraryIds, string tokenBase,
        KoboSyncToken syncToken, ServerSettingDto settings, SyncPageState state, CancellationToken ct)
    {
        if (state.RemainingSlots <= 0) return;

        var page = await UnsyncedEligibleChaptersQuery(userId, libraryIds)
            .OrderBy(c => c.LastModifiedUtc)
            .ThenBy(c => c.Id)
            .Take(state.RemainingSlots)
            .Include(c => c.Files)
            .Include(c => c.People).ThenInclude(p => p.Person)
            .Include(c => c.Volume).ThenInclude(v => v.Series).ThenInclude(s => s.Metadata)
            .ThenInclude(m => m!.People).ThenInclude(p => p.Person)
            .AsSplitQuery()
            .ToListAsync(ct);

        var pageIds = page.Select(c => c.Id).ToList();
        var progressByChapter = await LoadProgressByChapterAsync(userId, pageIds, ct);
        var locationByChapter = await LoadLocationsByChapterAsync(userId, pageIds, ct);

        foreach (var chapter in page)
        {
            var series = chapter.Volume.Series;
            var entitlementUuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
            var entitlement = await KoboEntitlementPayloadBuilder.BuildEntitlementPayloadAsync(chapter, series,
                entitlementUuid, tokenBase, isRemoved: false, preferKepub: settings.EnableKepubConversion,
                (id, source) => koboConversionService.TryGetCachedKepubPathAsync(id, source, ct));

            progressByChapter.TryGetValue(chapter.Id, out var progress);
            locationByChapter.TryGetValue(chapter.Id, out var location);
            if (TryAttachReadingState(entitlement, entitlementUuid, chapter, progress, location,
                    syncToken.ReadingStateLastModified, out var stateTs))
            {
                state.NestedReadingStateChapterIds.Add(chapter.Id);
                state.EmittedReadingStateChapterIds.Add(chapter.Id);
                if (stateTs > state.NewReadingStateLastModified) state.NewReadingStateLastModified = stateTs;
            }

            var created = KoboDateTime.CoalesceUtc(chapter.CreatedUtc, chapter.Created);
            var isNew = created > syncToken.BooksLastCreated;
            state.Items.Add(new JsonObject
            {
                [isNew ? "NewEntitlement" : "ChangedEntitlement"] = entitlement,
            });

            var modified = chapter.LastModifiedUtc == default ? chapter.LastModified : chapter.LastModifiedUtc;
            if (modified > state.NewBooksLastModified) state.NewBooksLastModified = modified;
            if (created > state.NewBooksLastCreated) state.NewBooksLastCreated = created;

            unitOfWork.DataContext.AppUserKoboSyncedChapter.Add(new AppUserKoboSyncedChapter
            {
                AppUserId = userId,
                ChapterId = chapter.Id,
            });

            // Queue background kepubify when this synced chapter still lacks a KEPUB artifact.
            if (settings.EnableKepubConversion)
            {
                var source = PreferNativeEpub(chapter.Files) ?? PreferConvertibleArchive(chapter.Files);
                if (source != null)
                {
                    await koboConversionService.EnqueueKepubifyIfNeededAsync(chapter.Id, source, ct);
                }
            }
        }

        state.RemainingSlots -= page.Count;
    }

    private async Task AppendChangedReadingStatesToPageAsync(int userId, KoboSyncToken syncToken,
        SyncPageState state, CancellationToken ct)
    {
        if (state.RemainingSlots <= 0) return;

        var changedSlots = await AppendChangedReadingStatesAsync(userId, state.Items, state.RemainingSlots,
            syncToken.ReadingStateLastModified, state.NestedReadingStateChapterIds, ct);
        foreach (var chapterId in changedSlots.EmittedChapterIds)
        {
            state.EmittedReadingStateChapterIds.Add(chapterId);
        }

        if (changedSlots.MaxReadingStateModified > state.NewReadingStateLastModified)
        {
            state.NewReadingStateLastModified = changedSlots.MaxReadingStateModified;
        }
    }

    private async Task<KoboLibrarySyncResult> FinalizeSyncTokenAsync(int userId,
        IReadOnlyCollection<int> libraryIds, KoboSyncToken syncToken, SyncPageState state, CancellationToken ct)
    {
        // Contiguous cursor: do not advance past an unemitted older reading-state.
        // Bump 1ms past the last emitted timestamp so float epoch round-trips cannot re-select it.
        var newReadingStateLastModified = await AdvanceReadingStateWatermarkAsync(userId,
            state.PageReadingStateWatermark, state.EmittedReadingStateChapterIds, ct);
        if (newReadingStateLastModified > state.PageReadingStateWatermark)
        {
            newReadingStateLastModified = newReadingStateLastModified.AddMilliseconds(1);
        }

        var remainingRemovals = await CountPendingRemovalsAsync(userId, ct);
        var remainingNew = await UnsyncedEligibleChaptersQuery(userId, libraryIds).CountAsync(ct);
        var remainingReadingStates = await CountChangedReadingStatesAsync(userId,
            newReadingStateLastModified, ct);
        var contSync = remainingRemovals + remainingNew + remainingReadingStates > 0;

        // Hold books_last_created until the final page so New vs Changed stays stable across continue pages.
        if (!contSync)
        {
            syncToken.BooksLastCreated = state.NewBooksLastCreated;
        }

        syncToken.BooksLastModified = state.NewBooksLastModified;
        syncToken.ArchiveLastModified = state.NewArchiveLastModified;
        syncToken.ReadingStateLastModified = newReadingStateLastModified;
        syncToken.TagsLastModified = state.NewTagsLastModified;

        return new KoboLibrarySyncResult
        {
            Items = state.Items,
            SyncToken = syncToken.ToHeaderValue(),
            Continue = contSync,
        };
    }

    /// <summary>Mutable accumulator for a single sync page: emitted items, remaining slots, watermarks.</summary>
    private sealed class SyncPageState
    {
        public SyncPageState(KoboSyncToken token, int pageSize)
        {
            RemainingSlots = pageSize;
            NewBooksLastModified = token.BooksLastModified;
            NewBooksLastCreated = token.BooksLastCreated;
            NewArchiveLastModified = token.ArchiveLastModified;
            NewReadingStateLastModified = token.ReadingStateLastModified;
            NewTagsLastModified = token.TagsLastModified;
            PageReadingStateWatermark = token.ReadingStateLastModified;
        }

        public JsonArray Items { get; } = new();
        public int RemainingSlots { get; set; }
        public DateTime NewBooksLastModified { get; set; }
        public DateTime NewBooksLastCreated { get; set; }
        public DateTime NewArchiveLastModified { get; set; }
        public DateTime NewReadingStateLastModified { get; set; }
        public DateTime NewTagsLastModified { get; set; }
        public DateTime PageReadingStateWatermark { get; }
        public HashSet<int> NestedReadingStateChapterIds { get; } = [];
        public HashSet<int> EmittedReadingStateChapterIds { get; } = [];
    }

    private static async Task<IDisposable> AcquireLibrarySyncLockAsync(int userId, CancellationToken ct)
    {
        var gate = LibrarySyncLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        var wait = SyncLockWaitOverride ?? TimeSpan.FromSeconds(SyncLockWaitSeconds);
        if (!await gate.WaitAsync(wait, ct))
        {
            throw new KavitaException(SyncBusyMessage);
        }

        return new LibrarySyncLockReleaser(gate);
    }

    private sealed class LibrarySyncLockReleaser(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            gate.Release();
        }
    }

    public async Task DeleteEntitlementAsync(string authToken, string entitlementId,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);
        if (!Guid.TryParse(entitlementId, out var entitlementGuid))
        {
            return;
        }

        var chapterId = await ResolveChapterIdByEntitlementAsync(userId, entitlementGuid, ct);
        if (chapterId <= 0)
        {
            // Already gone (or unknown): treat as success so the device can finish cleanup.
            return;
        }

        await ArchiveAndUnsyncAsync(userId, chapterId, isDeviceDeleted: true, ct);
        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>
    /// Clears the user's synced-set rows only. Does not rotate the AuthKey or clear archives.
    /// The next sync sees an empty synced-set and resets book + tags watermarks so shelves re-emit.
    /// </summary>
    public async Task ForceFullSyncAsync(int userId, CancellationToken ct = default)
    {
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.None, ct);
        if (user == null) throw new KavitaException("access-denied");

        var synced = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => s.AppUserId == userId)
            .ToListAsync(ct);
        if (synced.Count == 0) return;

        unitOfWork.DataContext.AppUserKoboSyncedChapter.RemoveRange(synced);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task PrepareTagDeleteAsync(Guid tagId, int ownerUserId, bool wasPromoted,
        CancellationToken ct = default)
    {
        List<int> recipientIds;
        if (wasPromoted)
        {
            recipientIds = await unitOfWork.DataContext.AppUser.Select(u => u.Id).ToListAsync(ct);
        }
        else
        {
            recipientIds = [ownerUserId];
        }

        if (recipientIds.Count == 0) return;

        var existing = await unitOfWork.DataContext.AppUserKoboTagTombstone
            .Where(t => t.TagId == tagId && recipientIds.Contains(t.AppUserId))
            .Select(t => t.AppUserId)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        var now = DateTime.UtcNow;
        var added = false;
        foreach (var recipientId in recipientIds)
        {
            if (!existingSet.Add(recipientId)) continue;
            unitOfWork.DataContext.AppUserKoboTagTombstone.Add(new AppUserKoboTagTombstone
            {
                AppUserId = recipientId,
                TagId = tagId,
                LastModifiedUtc = now,
            });
            added = true;
        }

        if (added)
        {
            await unitOfWork.CommitAsync(ct);
        }
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
        if (chapter == null) throw new KavitaException(EntitlementNotFoundMessage);

        var publicBase = BuildPublicBase(settings.HostName, settings.BaseUrl);
        var tokenBase = $"{publicBase}/{SyncPathPrefix}{authToken}";
        var uuid = KoboEntitlementId.FromChapterIdString(chapter.Id);
        var metadata = await KoboEntitlementPayloadBuilder.BuildBookMetadataAsync(chapter, chapter.Volume.Series,
            uuid, tokenBase, settings.EnableKepubConversion,
            (id, source) => koboConversionService.TryGetCachedKepubPathAsync(id, source, ct));
        return [metadata];
    }

    public async Task<KoboDownloadResult> GetDownloadAsync(string authToken, string entitlementId, string format,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        if (!IsSupportedDownloadFormat(format))
        {
            throw new KavitaException(FormatUnsupportedMessage);
        }

        var chapter = await ResolveEligibleChapterAsync(userId, entitlementId, ct);
        if (chapter == null) throw new KavitaException(EntitlementNotFoundMessage);

        if (IsKepubDownloadFormat(format))
        {
            return await GetKepubDownloadAsync(chapter, ct);
        }

        var epub = PreferNativeEpub(chapter.Files);
        if (epub != null)
        {
            return new KoboDownloadResult
            {
                FilePath = epub.FilePath,
                ContentType = downloadService.GetContentTypeFromFile(epub.FilePath),
                FileDownloadName = Path.GetFileName(epub.FilePath),
            };
        }

        var archive = PreferConvertibleArchive(chapter.Files);
        if (archive == null) throw new KavitaException(EpubMissingMessage);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var series = chapter.Volume.Series;
        var title = KoboEntitlementPayloadBuilder.BuildTitle(series, chapter);
        var convertedPath = await koboConversionService.GetOrConvertEpubAsync(chapter.Id, archive, title,
            settings.KoboConvertTimeBudgetSeconds, ct);

        return new KoboDownloadResult
        {
            FilePath = convertedPath,
            ContentType = downloadService.GetContentTypeFromFile(convertedPath),
            FileDownloadName = Path.GetFileName(convertedPath),
        };
    }

    private async Task<KoboDownloadResult> GetKepubDownloadAsync(Chapter chapter, CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.EnableKepubConversion)
        {
            throw new KavitaException(FormatUnsupportedMessage);
        }

        var source = PreferNativeEpub(chapter.Files) ?? PreferConvertibleArchive(chapter.Files);
        if (source == null) throw new KavitaException(EpubMissingMessage);

        var series = chapter.Volume.Series;
        var title = KoboEntitlementPayloadBuilder.BuildTitle(series, chapter);
        var kepubPath = await koboConversionService.GetOrConvertKepubAsync(chapter.Id, source, title,
            settings.KoboConvertTimeBudgetSeconds, ct);

        return new KoboDownloadResult
        {
            FilePath = kepubPath,
            ContentType = downloadService.GetContentTypeFromFile(kepubPath),
            FileDownloadName = BuildKepubDownloadFileName(source),
        };
    }

    public async Task<KoboCoverResult?> GetCoverAsync(string authToken, string entitlementId,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        var chapter = await ResolveAccessibleChapterAsync(userId, entitlementId, requireEligibleFormat: false, ct);
        if (chapter == null) return null;

        var coverFile = ResolveCoverFileName(chapter);
        if (string.IsNullOrEmpty(coverFile)) return null;

        var path = Path.Join(directoryService.CoverImageDirectory, coverFile);
        if (!File.Exists(path)) return null;

        return new KoboCoverResult { FilePath = path, ContentType = ResolveCoverContentType(path) };
    }

    private static string ResolveCoverContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };

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

    public async Task<object> GetReadingStateAsync(string authToken, string entitlementId,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        var chapter = await ResolveAccessibleChapterAsync(userId, entitlementId, requireEligibleFormat: false, ct);
        if (chapter == null)
        {
            return new JsonArray
            {
                KoboReadingStateMapper.BuildReadingState(entitlementId, DateTime.UtcNow, 0, 0, DateTime.UtcNow),
            };
        }

        var progress = await unitOfWork.AppUserProgressRepository.GetUserProgressAsync(chapter.Id, userId, ct);
        var location = await unitOfWork.DataContext.AppUserKoboReadingLocation
            .FirstOrDefaultAsync(l => l.AppUserId == userId && l.ChapterId == chapter.Id, ct);
        var created = KoboDateTime.CoalesceUtc(chapter.CreatedUtc, chapter.Created);
        var pagesRead = progress?.PagesRead ?? 0;
        var modified = progress?.LastModifiedUtc == default || progress == null
            ? created
            : progress.LastModifiedUtc;

        return new JsonArray
        {
            KoboReadingStateMapper.BuildReadingState(
                KoboEntitlementId.FromChapterIdString(chapter.Id),
                created,
                pagesRead,
                chapter.Pages,
                modified,
                location),
        };
    }

    public async Task<object> PutReadingStateAsync(string authToken, string entitlementId, JsonObject? body,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(authToken, ct);
        using var syncLock = await AcquireLibrarySyncLockAsync(userId, ct);

        if (body == null || body["ReadingStates"] is not JsonArray states || states.Count == 0
            || states[0] is not JsonObject readingState)
        {
            throw new KavitaException(ReadingStateMalformedMessage);
        }

        var bookmarkPresent = readingState["CurrentBookmark"] is JsonObject bookmark && bookmark.Count > 0;
        var statisticsPresent = readingState["Statistics"] is JsonObject stats && stats.Count > 0;
        var statusPresent = readingState["StatusInfo"] is JsonObject statusInfo && statusInfo.Count > 0;

        var chapter = await ResolveAccessibleChapterAsync(userId, entitlementId, requireEligibleFormat: false, ct);
        var now = DateTime.UtcNow;
        if (chapter == null)
        {
            return KoboReadingStateMapper.BuildPutSuccess(entitlementId, now,
                bookmarkPresent, statisticsPresent, statusPresent);
        }

        var existing = await unitOfWork.AppUserProgressRepository.GetUserProgressAsync(chapter.Id, userId, ct);
        var deviceTs = KoboDateTime.AsUtc(KoboReadingStateMapper.ResolveDeviceTimestamp(readingState, now));

        // Last-write-wins: keep server progress when it is strictly newer than the device timestamp.
        // Losing write ignored as a package (percent, status, BookScrollId, and Location).
        if (existing != null && KoboDateTime.AsUtc(existing.LastModifiedUtc) > deviceTs)
        {
            return KoboReadingStateMapper.BuildPutSuccess(entitlementId, KoboDateTime.AsUtc(existing.LastModifiedUtc),
                bookmarkPresent, statisticsPresent, statusPresent);
        }

        // Statistics are intentionally ignored (not persisted).
        var pagesRead = KoboReadingStateMapper.ResolvePagesRead(readingState, chapter.Pages,
            existing?.PagesRead ?? 0);
        var hasTruthyLocation = KoboReadingStateMapper.TryGetTruthyLocation(readingState,
            out var locationValue, out var locationType, out var locationSource);
        var locationWrite = new ReadingStateLocationWrite(hasTruthyLocation, locationValue, locationType,
            locationSource);

        if (existing == null && pagesRead == 0 && !statusPresent && !bookmarkPresent)
        {
            return KoboReadingStateMapper.BuildPutSuccess(entitlementId, now,
                bookmarkPresent, statisticsPresent, statusPresent);
        }

        existing = UpsertProgress(userId, chapter, existing, pagesRead);

        if (koboConvertProgressLocation.IsConvertChapter(chapter))
        {
            await ApplyConvertChapterLocationWinAsync(userId, chapter, existing, locationWrite, readingState, ct);
        }
        else if (locationWrite.HasTruthyLocation)
        {
            await ApplyLibraryChapterLocationWriteAsync(userId, chapter, existing, locationWrite, ct);
        }

        await unitOfWork.CommitAsync(ct);
        var savedTs = existing.LastModifiedUtc == default ? now : existing.LastModifiedUtc;
        return KoboReadingStateMapper.BuildPutSuccess(entitlementId, savedTs,
            bookmarkPresent, statisticsPresent, statusPresent);
    }

    /// <summary>Location columns parsed from a device reading-state PUT (truthy = write all three).</summary>
    private readonly record struct ReadingStateLocationWrite(bool HasTruthyLocation, string? Value, string? Type,
        string? Source);

    /// <summary>Creates or updates the user's progress row for a chapter and returns the tracked entity.</summary>
    private AppUserProgress UpsertProgress(int userId, Chapter chapter, AppUserProgress? existing, int pagesRead)
    {
        if (existing == null)
        {
            var series = chapter.Volume.Series;
            existing = new AppUserProgress
            {
                AppUserId = userId,
                ChapterId = chapter.Id,
                VolumeId = chapter.VolumeId,
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                PagesRead = pagesRead,
            };
            unitOfWork.DataContext.AppUserProgresses.Add(existing);
        }
        else
        {
            existing.PagesRead = pagesRead;
            existing.VolumeId = chapter.VolumeId;
            existing.SeriesId = chapter.Volume.SeriesId;
            existing.LibraryId = chapter.Volume.Series.LibraryId;
            unitOfWork.AppUserProgressRepository.Update(existing);
        }

        return existing;
    }

    /// <summary>
    /// Library (non-convert) chapters: a truthy Location writes all three columns and best-effort maps
    /// the Location to a BookScrollId when it resolves inside the library EPUB.
    /// </summary>
    private async Task ApplyLibraryChapterLocationWriteAsync(int userId, Chapter chapter, AppUserProgress existing,
        ReadingStateLocationWrite location, CancellationToken ct)
    {
        // Truthy Location writes all three columns; falsy/absent leaves prior columns unchanged.
        var locationRow = await unitOfWork.DataContext.AppUserKoboReadingLocation
            .FirstOrDefaultAsync(l => l.AppUserId == userId && l.ChapterId == chapter.Id, ct);
        if (locationRow == null)
        {
            unitOfWork.DataContext.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
            {
                AppUserId = userId,
                ChapterId = chapter.Id,
                LocationValue = location.Value,
                LocationType = location.Type,
                LocationSource = location.Source,
            });
        }
        else
        {
            locationRow.LocationValue = location.Value;
            locationRow.LocationType = location.Type;
            locationRow.LocationSource = location.Source;
        }

        // Best-effort Location → BookScrollId when valid in the library EPUB; else leave.
        var libraryEpub = koboLocationMapper.ResolveLibraryEpubPath(chapter);
        var mappedScroll = await koboLocationMapper.TryMapLocationToBookScrollIdAsync(
            libraryEpub, location.Value, location.Type, location.Source, ct);
        if (!string.IsNullOrEmpty(mappedScroll))
        {
            existing.BookScrollId = mappedScroll;
        }
    }

    /// <summary>
    /// Convert chapters: after a winning write, always refresh Location (upsert encode or clear).
    /// Never leave a stale convert Location on falsy/absent/invalid device Location.
    /// </summary>
    private async Task ApplyConvertChapterLocationWinAsync(int userId, Chapter chapter, AppUserProgress existing,
        ReadingStateLocationWrite location, JsonObject readingState, CancellationToken ct)
    {
        var kepub = await koboConvertProgressLocation.TryResolveSpineAlignedKepubPathAsync(chapter, ct);
        var readyToRead = string.Equals(
            (readingState["StatusInfo"] as JsonObject)?["Status"]?.GetValue<string>(),
            KoboReadingStateMapper.StatusReadyToRead,
            StringComparison.OrdinalIgnoreCase);

        if (location.HasTruthyLocation)
        {
            if (kepub != null &&
                KoboConvertLocationCodec.TryDecode(location.Value, location.Type, location.Source, chapter.Pages,
                    out var decodedPages))
            {
                await koboConvertProgressLocation.UpsertLocationAsync(userId, chapter.Id,
                    location.Value, location.Type, location.Source, ct);
                existing.PagesRead = decodedPages;
                return;
            }

            // Invalid device Location: clear then encode from winning PagesRead when KEPUB exists.
            if (kepub == null)
            {
                await koboConvertProgressLocation.ClearLocationAsync(userId, chapter.Id, ct);
                return;
            }

            await koboConvertProgressLocation.UpsertFromPagesReadAsync(userId, chapter, existing.PagesRead,
                readyToRead, ct);
            return;
        }

        // Falsy/absent Location: do not leave prior convert Location.
        if (kepub == null)
        {
            await koboConvertProgressLocation.ClearLocationAsync(userId, chapter.Id, ct);
            return;
        }

        await koboConvertProgressLocation.UpsertFromPagesReadAsync(userId, chapter, existing.PagesRead,
            readyToRead, ct);
    }

    private async Task<Dictionary<int, AppUserProgress>> LoadProgressByChapterAsync(int userId,
        IReadOnlyCollection<int> chapterIds, CancellationToken ct)
    {
        if (chapterIds.Count == 0) return new Dictionary<int, AppUserProgress>();
        return await unitOfWork.DataContext.AppUserProgresses
            .Where(p => p.AppUserId == userId && chapterIds.Contains(p.ChapterId))
            .ToDictionaryAsync(p => p.ChapterId, ct);
    }

    private async Task<Dictionary<int, AppUserKoboReadingLocation>> LoadLocationsByChapterAsync(int userId,
        IReadOnlyCollection<int> chapterIds, CancellationToken ct)
    {
        if (chapterIds.Count == 0) return new Dictionary<int, AppUserKoboReadingLocation>();
        return await unitOfWork.DataContext.AppUserKoboReadingLocation
            .Where(l => l.AppUserId == userId && chapterIds.Contains(l.ChapterId))
            .ToDictionaryAsync(l => l.ChapterId, ct);
    }

    private static bool TryAttachReadingState(JsonObject entitlement, string entitlementUuid, Chapter chapter,
        AppUserProgress? progress, AppUserKoboReadingLocation? location, DateTime readingStateWatermark,
        out DateTime stateTimestamp)
    {
        stateTimestamp = default;
        if (progress == null) return false;
        var modified = KoboDateTime.AsUtc(KoboDateTime.CoalesceUtc(progress.LastModifiedUtc, progress.LastModified));
        if (modified <= KoboDateTime.AsUtc(readingStateWatermark)) return false;

        var created = KoboDateTime.CoalesceUtc(chapter.CreatedUtc, chapter.Created);
        entitlement["ReadingState"] = KoboReadingStateMapper.BuildReadingState(entitlementUuid, created,
            progress.PagesRead, chapter.Pages, modified, location);
        stateTimestamp = modified;
        return true;
    }

    private async Task<(int Emitted, DateTime MaxReadingStateModified, List<int> EmittedChapterIds)>
        AppendChangedReadingStatesAsync(
        int userId, JsonArray items, int remainingSlots, DateTime readingStateWatermark,
        HashSet<int> excludeChapterIds, CancellationToken ct)
    {
        if (remainingSlots <= 0)
        {
            return (0, readingStateWatermark, []);
        }

        var candidates = await PendingReadingStateQuery(userId, readingStateWatermark)
            .Where(p => !excludeChapterIds.Contains(p.ChapterId))
            .OrderBy(p => p.LastModifiedUtc)
            .ThenBy(p => p.ChapterId)
            .Take(remainingSlots)
            .Select(p => new { p.ChapterId, p.PagesRead, p.LastModifiedUtc })
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return (0, readingStateWatermark, []);
        }

        var chapterIds = candidates.Select(c => c.ChapterId).ToList();
        var chapters = await unitOfWork.DataContext.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Pages, c.CreatedUtc, c.Created })
            .ToListAsync(ct);
        var chapterById = chapters.ToDictionary(c => c.Id);
        var locationByChapter = await LoadLocationsByChapterAsync(userId, chapterIds, ct);

        var maxModified = readingStateWatermark;
        var emittedIds = new List<int>();
        foreach (var row in candidates)
        {
            if (!chapterById.TryGetValue(row.ChapterId, out var chapter)) continue;
            var uuid = KoboEntitlementId.FromChapterIdString(row.ChapterId);
            var created = KoboDateTime.CoalesceUtc(chapter.CreatedUtc, chapter.Created);
            var modified = row.LastModifiedUtc;
            locationByChapter.TryGetValue(row.ChapterId, out var location);
            var readingState = KoboReadingStateMapper.BuildReadingState(uuid, created, row.PagesRead,
                chapter.Pages, modified, location);
            items.Add(new JsonObject
            {
                ["ChangedReadingState"] = new JsonObject
                {
                    ["ReadingState"] = readingState,
                },
            });
            if (modified > maxModified) maxModified = modified;
            emittedIds.Add(row.ChapterId);
        }

        return (emittedIds.Count, maxModified, emittedIds);
    }

    private async Task<DateTime> AdvanceReadingStateWatermarkAsync(int userId, DateTime previousWatermark,
        HashSet<int> emittedChapterIds, CancellationToken ct)
    {
        if (emittedChapterIds.Count == 0) return previousWatermark;

        var pending = await PendingReadingStateQuery(userId, previousWatermark)
            .OrderBy(p => p.LastModifiedUtc)
            .ThenBy(p => p.ChapterId)
            .Select(p => new { p.ChapterId, p.LastModifiedUtc })
            .ToListAsync(ct);

        var watermark = previousWatermark;
        foreach (var row in pending)
        {
            if (!emittedChapterIds.Contains(row.ChapterId)) break;
            var modified = KoboDateTime.AsUtc(row.LastModifiedUtc);
            if (modified > watermark) watermark = modified;
        }

        return watermark;
    }

    private async Task<int> CountChangedReadingStatesAsync(int userId, DateTime readingStateWatermark,
        CancellationToken ct) =>
        await PendingReadingStateQuery(userId, readingStateWatermark).CountAsync(ct);


    private IQueryable<AppUserProgress> PendingReadingStateQuery(int userId, DateTime readingStateWatermark)
    {
        var watermark = KoboDateTime.AsUtc(readingStateWatermark);
        return unitOfWork.DataContext.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Where(p => p.LastModifiedUtc > watermark)
            .Where(p => unitOfWork.DataContext.AppUserKoboSyncedChapter
                .Any(s => s.AppUserId == userId && s.ChapterId == p.ChapterId));
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
        // Native EPUB or CBZ/CBR (extension / path). PDF-only is excluded.
        return unitOfWork.DataContext.Chapter
            .Where(c => libraryIds.Contains(c.Volume.Series.LibraryId))
            .Where(KoboEligibleFormats.ChapterHasEligibleFile);
    }

    private async Task<(int Emitted, DateTime MaxArchiveModified)> AppendArchiveRemovalsAsync(
        int userId, string tokenBase, KoboSyncToken syncToken, JsonArray items, int limit,
        bool preferKepub, CancellationToken ct)
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
            var entitlement = await KoboEntitlementPayloadBuilder.BuildEntitlementPayloadAsync(chapter, series,
                entitlementUuid, tokenBase, isRemoved: true, preferKepub: preferKepub,
                (id, source) => koboConversionService.TryGetCachedKepubPathAsync(id, source, ct));

            var created = KoboDateTime.CoalesceUtc(chapter.CreatedUtc, chapter.Created);
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
            var entitlement = KoboEntitlementPayloadBuilder.BuildTombstoneEntitlementPayload(tombstone,
                entitlementUuid);
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

    private async Task<int> ResolveChapterIdByEntitlementAsync(int userId, Guid entitlementGuid,
        CancellationToken ct)
    {
        // Prefer exact match via this user's synced/archived rows, then scan accessible libraries.
        var knownIds = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => s.AppUserId == userId)
            .Select(s => s.ChapterId)
            .Union(unitOfWork.DataContext.AppUserKoboArchivedChapter
                .Where(a => a.AppUserId == userId)
                .Select(a => a.ChapterId))
            .Distinct()
            .ToListAsync(ct);
        var fromKnown = knownIds.FirstOrDefault(id => KoboEntitlementId.FromChapterId(id) == entitlementGuid);
        if (fromKnown > 0) return fromKnown;

        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        if (libraryIds.Count == 0) return 0;

        var candidateIds = await unitOfWork.DataContext.Chapter
            .Where(c => libraryIds.Contains(c.Volume.Series.LibraryId))
            .Select(c => c.Id)
            .ToListAsync(ct);
        return candidateIds.FirstOrDefault(id => KoboEntitlementId.FromChapterId(id) == entitlementGuid);
    }

    private async Task<Chapter?> ResolveEligibleChapterAsync(int userId, string entitlementId,
        CancellationToken ct) =>
        await ResolveAccessibleChapterAsync(userId, entitlementId, requireEligibleFormat: true, ct);

    private async Task<Chapter?> ResolveAccessibleChapterAsync(int userId, string entitlementId,
        bool requireEligibleFormat, CancellationToken ct)
    {
        if (!Guid.TryParse(entitlementId, out var entitlementGuid)) return null;

        var libraryIds = await GetAllowedLibraryIdsAsync(userId, ct);
        if (libraryIds.Count == 0) return null;

        var query = unitOfWork.DataContext.Chapter
            .Where(c => libraryIds.Contains(c.Volume.Series.LibraryId));
        if (requireEligibleFormat)
        {
            query = query.Where(KoboEligibleFormats.ChapterHasEligibleFile);
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

    private static bool IsSupportedDownloadFormat(string format) =>
        IsEpubDownloadFormat(format) || IsKepubDownloadFormat(format);

    private static bool IsEpubDownloadFormat(string format) =>
        string.Equals(format, "epub", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, EpubFormat, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, "epub3", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, Epub3Format, StringComparison.OrdinalIgnoreCase);

    private static bool IsKepubDownloadFormat(string format) =>
        string.Equals(format, "kepub", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, KepubFormat, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Kobo clients expect Content-Disposition to end in <c>.kepub.epub</c> for sync downloads.
    /// </summary>
    internal static string BuildKepubDownloadFileName(MangaFile sourceFile)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        var stem = Path.GetFileNameWithoutExtension(sourceFile.FileName);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = Path.GetFileNameWithoutExtension(sourceFile.FilePath);
        }

        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "book";
        }

        return stem + KepubDownloadFileExtension;
    }

    internal static MangaFile? PreferNativeEpub(IEnumerable<MangaFile> files) =>
        KoboEligibleFormats.PreferNativeEpub(files);

    /// <summary>
    /// First CBZ/CBR archive file when present. Native EPUB preference is handled by callers.
    /// </summary>
    internal static MangaFile? PreferConvertibleArchive(IEnumerable<MangaFile> files) =>
        KoboEligibleFormats.PreferConvertibleArchive(files);

    internal static bool IsConvertibleArchive(MangaFile file) =>
        KoboEligibleFormats.IsConvertibleArchive(file);

    private static string? ResolveCoverFileName(Chapter chapter)
    {
        if (!string.IsNullOrEmpty(chapter.CoverImage)) return chapter.CoverImage;
        if (!string.IsNullOrEmpty(chapter.Volume?.CoverImage)) return chapter.Volume.CoverImage;
        return chapter.Volume?.Series?.CoverImage;
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
