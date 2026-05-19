using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Common.Helpers;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.KavitaPlus;
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

        var matchedSeriesCount = await context.Series
            .CountAsync(s => s.MangaBakaId != 0, ct);

        var totalEligibleSeriesCount = await context.Series
            .Include(s => s.Library)
            .CountAsync(s => s.Library.AllowMetadataMatching, ct);

        var scrobbleQueueCount = await context.ScrobbleEvent
            .CountAsync(e => !e.IsProcessed, ct);

        return new KavitaPlusAuditStatsDto
        {
            Events24h = events24H,
            Failures24h = failures24H,
            UnresolvedMatchFailures = unresolvedMatchFailures,
            MatchedSeriesCount = matchedSeriesCount,
            TotalEligibleSeriesCount = totalEligibleSeriesCount,
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

        var recentEvents = recentRaw.Select(MapToDto).ToList();

        return new KavitaPlusAuditSeriesInfoDto
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            SeriesName = series.Name,
            IsMatched = series.MangaBakaId != 0,
            MangaBakaId = series.MangaBakaId != 0 ? series.MangaBakaId : null,
            AniListId = series.AniListId != 0 ? series.AniListId : null,
            HardcoverId = series.HardcoverId != 0 ? series.HardcoverId : null,
            CbrId = series.CbrId != 0 ? series.CbrId : null,
            ComicVineId = series.ComicVineId != string.Empty ? series.ComicVineId : null,
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
        IList<MetadataFieldChange>? diff = null;
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
                        Rating = p.Rating,
                        Provider = ScrobbleProvider.AniList, // TODO: This needs to allow provider to be passed from ScrobbleService (Amelia)
                        LibraryType = p.LibraryType,
                    };
                }
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
            CanRetry = e.Status == AuditStatus.Failure
                       && e.Category == KavitaPlusAuditCategory.Scrobble
                       && !e.HasRetried,
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
        public List<MetadataFieldChange>? Changes { get; set; }
    }
}
