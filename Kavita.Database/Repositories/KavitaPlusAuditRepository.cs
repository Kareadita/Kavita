using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.API.Services.Plus;
using Kavita.Common.Helpers;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.History;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class KavitaPlusAuditRepository(DataContext context) : IKavitaPlusAuditRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(KavitaPlusAuditLog entry) => context.KavitaPlusAuditLogs.Add(entry);

    public async Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        await context.KavitaPlusAuditLogs
            .Where(e => e.CreatedUtc < cutoff)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<PagedList<KavitaPlusAuditEntryDto>> GetPagedAsync(
        KavitaPlusAuditFilterDto filter, UserParams userParams, CancellationToken ct = default)
    {
        var query = BuildBaseQuery(filter);
        return await ProjectAndPage(query, userParams, ct);
    }

    public async Task<PagedList<KavitaPlusAuditEntryDto>> GetMyActivityAsync(
        int userId, KavitaPlusAuditFilterDto filter, UserParams userParams, CancellationToken ct = default)
    {
        var query = BuildBaseQuery(filter)
            .Where(e => e.UserId == userId);

        return await ProjectAndPage(query, userParams, ct);
    }

    public async Task<KavitaPlusAuditStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var cutoff24H = DateTime.UtcNow.AddHours(-24);

        var events24H = await context.KavitaPlusAuditLogs
            .CountAsync(e => e.CreatedUtc >= cutoff24H, ct);

        var failures24H = await context.KavitaPlusAuditLogs
            .CountAsync(e => e.CreatedUtc >= cutoff24H && e.Status == AuditStatus.Failure, ct);

        var unresolvedMatchFailures = await context.KavitaPlusAuditLogs
            .CountAsync(e => e.EventType == KavitaPlusEventType.SeriesMatchFailed
                             && e.Status == AuditStatus.Failure, ct);

        var baseEligible = context.Series
            .Where(s => !IExternalMetadataService.NonEligibleLibraryTypes.Contains(s.Library.Type))
            .Where(s => s.Library.AllowMetadataMatching)
            .Where(s => !s.DontMatch);

        var matchedSeriesCount = await baseEligible.WhereMatchedExternalMetadata().CountAsync(ct);

        var totalEligibleSeriesCount = await baseEligible.CountAsync(ct);

        var staleMatchesCount = await baseEligible.WhereStaleExternalMetadata().CountAsync(ct);

        var blacklistedSeriesCount = await baseEligible
            .Where(s => s.IsBlacklisted)
            .CountAsync(ct);

        var scrobbleQueueCount = await context.ScrobbleEvent
            .CountAsync(e => !e.IsProcessed, ct);

        return new KavitaPlusAuditStatsDto
        {
            Events24H = events24H,
            Failures24H = failures24H,
            UnresolvedMatchFailures = unresolvedMatchFailures,
            MatchedSeriesCount = matchedSeriesCount,
            TotalEligibleSeriesCount = totalEligibleSeriesCount,
            StaleMatchesCount = staleMatchesCount,
            BlacklistedSeriesCount = blacklistedSeriesCount,
            ScrobbleQueueCount = scrobbleQueueCount,
        };
    }

    public async Task<KavitaPlusAuditSeriesInfoDto> GetSeriesInfoAsync(
        int seriesId, int callingUserId, bool isAdmin, CancellationToken ct = default)
    {
        var series = await context.Series
            .Include(s => s.ExternalSeriesMetadata)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct);

        if (series == null)
        {
            return new KavitaPlusAuditSeriesInfoDto { SeriesId = seriesId };
        }

        var recentQuery = context.KavitaPlusAuditLogs
            .AsNoTracking()
            .Where(e => e.SeriesId == seriesId)
            .Where(e => e.Category != KavitaPlusAuditCategory.Scrobble
                        || isAdmin
                        || e.UserId == callingUserId)
            .OrderByDescending(e => e.CreatedUtc)
            .Take(20);

        var recentRaw = await recentQuery
            .Select(e => new RawEntry(
                e.Id, e.CreatedUtc, e.Category, e.EventType, e.Status,
                e.SeriesId, series.LibraryId, series.Name,
                e.SubjectType, e.SubjectId,
                e.UserId, e.User != null ? e.User.UserName : null,
                e.Payload, e.ErrorMessage, e.HasRetried))
            .ToListAsync(ct);

        // Due to Json deserialization, I can't use automapper here and need to do in-mem
        var recentEvents = recentRaw.Select(MapToDto).ToList();

        return new KavitaPlusAuditSeriesInfoDto
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            SeriesName = series.Name,
            IsMatched = !series.IsBlacklisted
                && series.ExternalSeriesMetadata != null
                && series.ExternalSeriesMetadata.ValidUntilUtc > DateTime.MinValue,
            MangaBakaId = series.MangaBakaId != 0 ? series.MangaBakaId : null,
            AniListId = series.AniListId != 0 ? series.AniListId : null,
            HardcoverId = series.HardcoverId != 0 ? series.HardcoverId : null,
            CbrId = series.CbrId != 0 ? series.CbrId : null,
            ComicVineId = series.ComicVineId != string.Empty ? series.ComicVineId : null,
            MalId = series.MalId != 0 ? series.MalId : null,
            MetronId = series.MetronId != 0 ? series.MetronId : null,
            MetadataProvider = series.ExternalSeriesMetadata?.Provider,
            NextRefreshUtc = series.ExternalSeriesMetadata?.ValidUntilUtc,
            LastRefreshedUtc = series.ExternalSeriesMetadata?.LastModifiedUtc,
            RecentEvents = recentEvents,
        };
    }

    private IQueryable<KavitaPlusAuditLog> BuildBaseQuery(KavitaPlusAuditFilterDto filter)
    {
        return context.KavitaPlusAuditLogs
            .AsNoTracking()
            .WhereIf(filter.Category.HasValue, e => e.Category == filter.Category!.Value)
            .WhereIf(filter.Status.HasValue, e => e.Status == filter.Status!.Value)
            .WhereIf(filter.SubjectType.HasValue, e => e.SubjectType == filter.SubjectType!.Value)
            .WhereIf(filter.UserId.HasValue, e => e.UserId == filter.UserId!.Value)
            .WhereIf(filter.SeriesId.HasValue, e => e.SeriesId == filter.SeriesId!.Value)
            .WhereIf(filter.Provider.HasValue, e =>
                e.Category == KavitaPlusAuditCategory.Scrobble &&
                // Best way for us to filter right now. In EF.Core 11 we'll get a EF.Functions.JsonContains but unsure
                // if this is also for sqlite
                EF.Functions.Like(e.Payload, $"%\"Provider\":{(int)filter.Provider!.Value}%"))
            .WhereIf(filter.FromUtc.HasValue, e => e.CreatedUtc >= filter.FromUtc!.Value)
            .WhereIf(filter.ToUtc.HasValue, e => e.CreatedUtc <= filter.ToUtc!.Value)
            .WhereIf(!string.IsNullOrEmpty(filter.Search), e =>
                context.Series.Any(s => s.Id == e.SeriesId && s.Name.Contains(filter.Search!)) ||
                (e.User != null && e.User.UserName!.Contains(filter.Search!)) ||
                (e.ErrorMessage != null && e.ErrorMessage.Contains(filter.Search!)))
            .OrderByDescending(e => e.CreatedUtc);
    }

    private async Task<PagedList<KavitaPlusAuditEntryDto>> ProjectAndPage(
        IQueryable<KavitaPlusAuditLog> query, UserParams userParams, CancellationToken ct)
    {
        var count = await query.CountAsync(ct);
        var raw = await query
            .Skip((userParams.PageNumber - 1) * userParams.PageSize)
            .Take(userParams.PageSize)
            .Select(e => new RawEntry(
                e.Id, e.CreatedUtc, e.Category, e.EventType, e.Status,
                e.SeriesId,
                context.Series.Where(s => s.Id == e.SeriesId).Select(s => (int?)s.LibraryId).FirstOrDefault(),
                context.Series.Where(s => s.Id == e.SeriesId).Select(s => s.Name).FirstOrDefault(),
                e.SubjectType, e.SubjectId,
                e.UserId, e.User != null ? e.User.UserName : null,
                e.Payload, e.ErrorMessage, e.HasRetried))
            .ToListAsync(ct);

        var items = raw.Select(MapToDto).ToList();
        return PagedList<KavitaPlusAuditEntryDto>.Create(items, count, userParams);
    }

    private static KavitaPlusAuditEntryDto MapToDto(RawEntry e)
    {
        IList<MetadataFieldChangeDto>? diff = null;
        if (e is {Category: KavitaPlusAuditCategory.Metadata, Payload: not null})
        {
            try
            {
                var wrapper = JsonSerializer.Deserialize<ChangesWrapper>(e.Payload, JsonOptions);
                diff = wrapper?.Changes;
            }
            catch
            {
                // malformed payload
            }
        }

        KavitaPlusScrobbleDetailsDto? scrobbleDetails = null;
        if (e is {Category: KavitaPlusAuditCategory.Scrobble, Payload: not null})
        {
            try
            {
                var p = JsonSerializer.Deserialize<AuditLogScrobbleParamsDto>(e.Payload, JsonOptions);
                if (p != null)
                {
                    scrobbleDetails = new KavitaPlusScrobbleDetailsDto
                    {
                        ScrobbleEventType = p.ScrobbleEventType,
                        ChapterNumber = p.ChapterNumber,
                        VolumeNumber = p.VolumeNumber,
                        PercentRead = p.PercentRead,
                        Rating = p.Rating,
                        ReviewBody = p.ReviewBody,
                        ReadStatus = p.ReadStatus,
                        Provider = p.Provider,
                        LibraryType = p.LibraryType,
                    };
                }
            }
            catch
            {
                // malformed payload
            }
        }

        KavitaPlusAuditMatchDetailsDto? matchDetails = null;
        if (e is { Category: KavitaPlusAuditCategory.Match, Payload: not null })
        {
            try
            {
                matchDetails = e.EventType switch
                {
                    KavitaPlusEventType.SeriesMatched =>
                        KavitaPlusAuditMatchDetailsDto.From(JsonSerializer.Deserialize<AuditLogMatchedParamsDto>(e.Payload, JsonOptions)),
                    KavitaPlusEventType.SeriesMatchFixed =>
                        KavitaPlusAuditMatchDetailsDto.From(JsonSerializer.Deserialize<AuditLogMatchClearedParamsDto>(e.Payload, JsonOptions)),
                    KavitaPlusEventType.SeriesMatchFailed or KavitaPlusEventType.SeriesBlacklisted =>
                        KavitaPlusAuditMatchDetailsDto.From(JsonSerializer.Deserialize<AuditLogMatchFailureParamsDto>(e.Payload, JsonOptions)),
                    KavitaPlusEventType.SeriesDontMatchSet =>
                        KavitaPlusAuditMatchDetailsDto.From(JsonSerializer.Deserialize<AuditLogMatchDontMatchParamsDto>(e.Payload, JsonOptions)),
                    _ => null
                };
            }
            catch
            {
                // malformed payload
            }
        }

        KavitaPlusAuditSyncDetailsDto? syncDetails = null;
        if (e is { Category: KavitaPlusAuditCategory.Sync, Payload: not null })
        {
            try
            {
                switch (e.EventType)
                {
                    case KavitaPlusEventType.CollectionSynced:
                        syncDetails = KavitaPlusAuditSyncDetailsDto.From(JsonSerializer.Deserialize<AuditLogCollectionSyncedParamsDto>(e.Payload, JsonOptions));
                        break;
                    case KavitaPlusEventType.CollectionItemAdded:
                        syncDetails = KavitaPlusAuditSyncDetailsDto.From(JsonSerializer.Deserialize<AuditLogCollectionItemParamsDto>(e.Payload, JsonOptions));
                        break;
                    case KavitaPlusEventType.SyncCompleted:
                        syncDetails = KavitaPlusAuditSyncDetailsDto.From(JsonSerializer.Deserialize<AuditLogWantToReadSyncCompletedParamsDto>(e.Payload, JsonOptions));
                        break;
                    case KavitaPlusEventType.SyncStarted:
                    {
                        var started = JsonSerializer.Deserialize<AuditLogCollectionStartedParamsDto>(e.Payload, JsonOptions);
                        syncDetails = !string.IsNullOrEmpty(started?.CollectionName)
                            ? KavitaPlusAuditSyncDetailsDto.From(started)
                            : KavitaPlusAuditSyncDetailsDto.From(JsonSerializer.Deserialize<AuditLogWantToReadSyncParamsDto>(e.Payload, JsonOptions));
                        break;
                    }
                    case KavitaPlusEventType.SyncFailed:
                        syncDetails = KavitaPlusAuditSyncDetailsDto.From(JsonSerializer.Deserialize<AuditLogCollectionFailedParamsDto>(e.Payload, JsonOptions));
                        break;
                }
            }
            catch
            {
                // malformed payload
            }
        }

        KavitaPlusAuditMetadataExtrasDto? metadataExtras = null;
        if (e is { Category: KavitaPlusAuditCategory.Metadata, Payload: not null })
        {
            try
            {
                metadataExtras = e.EventType switch
                {
                    KavitaPlusEventType.CoverUpdated =>
                        KavitaPlusAuditMetadataExtrasDto.From(JsonSerializer.Deserialize<AuditLogSeriesCoverParamsDto>(e.Payload, JsonOptions)),
                    KavitaPlusEventType.ChapterCoverUpdated =>
                        KavitaPlusAuditMetadataExtrasDto.From(JsonSerializer.Deserialize<AuditLogChapterCoverParamsDto>(e.Payload, JsonOptions)),
                    KavitaPlusEventType.PersonAliasAdded =>
                        KavitaPlusAuditMetadataExtrasDto.From(JsonSerializer.Deserialize<AuditLogPersonAliasParamsDto>(e.Payload, JsonOptions)),
                    KavitaPlusEventType.PersonCoverUpdated =>
                        KavitaPlusAuditMetadataExtrasDto.From(JsonSerializer.Deserialize<AuditLogPersonCoverParamsDto>(e.Payload, JsonOptions)),
                    KavitaPlusEventType.MetadataFetched =>
                        KavitaPlusAuditMetadataExtrasDto.From(JsonSerializer.Deserialize<AuditLogMetadataFetchParamsDto>(e.Payload, JsonOptions)),
                    _ => null
                };
            }
            catch
            {
                // malformed payload
            }
        }

        return new KavitaPlusAuditEntryDto
        {
            Id = e.Id,
            CreatedUtc = e.CreatedUtc,
            Category = e.Category,
            EventType = e.EventType,
            Status = e.Status,
            SeriesId = e.SeriesId,
            LibraryId = e.LibraryId,
            SeriesName = e.SeriesName,
            SubjectType = e.SubjectType,
            SubjectId = e.SubjectId,
            UserId = e.UserId,
            Username = e.Username,
            Diff = diff,
            ErrorMessage = e.ErrorMessage,
            ScrobbleDetails = scrobbleDetails,
            MatchDetails = matchDetails,
            SyncDetails = syncDetails,
            MetadataExtras = metadataExtras,
            CanRetry = e is { Status: AuditStatus.Failure, Category: KavitaPlusAuditCategory.Scrobble, HasRetried: false },
        };
    }

    public async Task MarkAsRetriedAsync(long id, CancellationToken ct = default)
    {
        await context.KavitaPlusAuditLogs
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.HasRetried, true), ct);
    }

    private sealed record RawEntry(
        long Id, DateTime CreatedUtc, KavitaPlusAuditCategory Category,
        KavitaPlusEventType EventType, AuditStatus Status,
        int? SeriesId, int? LibraryId, string? SeriesName,
        AuditSubjectType SubjectType, int? SubjectId,
        int? UserId, string? Username,
        string? Payload, string? ErrorMessage, bool HasRetried);

    private sealed class ChangesWrapper
    {
        public List<MetadataFieldChangeDto>? Changes { get; set; }
    }
}
