using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.History;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

public class KavitaPlusAuditService(IUnitOfWork unitOfWork, ILogger<KavitaPlusAuditService> logger)
    : IKavitaPlusAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private const int RetentionDays = 90;

    public async Task LogAsync(
        KavitaPlusAuditCategory category,
        KavitaPlusEventType eventType,
        AuditStatus status,
        AuditSubjectType subjectType = AuditSubjectType.Global,
        int? seriesId = null,
        int? subjectId = null,
        object? payload = null,
        string? error = null,
        int? userId = null,
        CancellationToken ct = default)
    {
        try
        {
            var entry = new KavitaPlusAuditLog
            {
                CreatedUtc = DateTime.UtcNow,
                Category = category,
                EventType = eventType,
                Status = status,
                SubjectType = subjectType,
                SeriesId = seriesId,
                SubjectId = subjectId,
                Payload = payload != null ? JsonSerializer.Serialize(payload, JsonOptions) : null,
                ErrorMessage = error,
                UserId = userId,
            };
            unitOfWork.KavitaPlusAuditRepository.Add(entry);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit failures must never surface to callers
            logger.LogWarning(ex, "[Kavita+ Audit] Failed to write audit entry {EventType}", eventType);
        }
    }

    public Task LogMatchAsync(KavitaPlusEventType type, int seriesId, object payload,
        AuditStatus status = AuditStatus.Success, string? error = null, CancellationToken ct = default) =>
        LogAsync(KavitaPlusAuditCategory.Match, type, status,
            AuditSubjectType.Series, seriesId: seriesId, payload: payload, error: error, ct: ct);

    public Task LogMetadataAsync(int seriesId, IList<MetadataFieldChangeDto> changes, CancellationToken ct = default) =>
        LogAsync(KavitaPlusAuditCategory.Metadata, KavitaPlusEventType.MetadataUpdated, AuditStatus.Success,
            AuditSubjectType.Series, seriesId: seriesId, payload: new AuditLogMetadataChangesParamsDto { Changes = changes }, ct: ct);

    public Task LogChapterMetadataAsync(int chapterId, int seriesId, IList<MetadataFieldChangeDto> changes,
        CancellationToken ct = default) =>
        LogAsync(KavitaPlusAuditCategory.Metadata, KavitaPlusEventType.ChapterMetadataUpdated, AuditStatus.Success,
            AuditSubjectType.Chapter, seriesId: seriesId, subjectId: chapterId, payload: new AuditLogMetadataChangesParamsDto { Changes = changes }, ct: ct);

    public Task LogPersonAsync(KavitaPlusEventType type, int personId, object payload,
        AuditStatus status = AuditStatus.Success, CancellationToken ct = default) =>
        LogAsync(KavitaPlusAuditCategory.Metadata, type, status,
            AuditSubjectType.Person, subjectId: personId, payload: payload, ct: ct);

    public Task LogCollectionAsync(KavitaPlusEventType type, int collectionId, object payload,
        AuditStatus status = AuditStatus.Success, int? userId = null, CancellationToken ct = default) =>
        LogAsync(KavitaPlusAuditCategory.Sync, type, status,
            AuditSubjectType.Collection, subjectId: collectionId, payload: payload, userId: userId, ct: ct);

    public Task LogScrobbleAsync(KavitaPlusEventType type, int seriesId, AuditLogScrobbleParamsDto details,
        AuditStatus status, string? error = null, int? userId = null, CancellationToken ct = default) =>
        LogAsync(KavitaPlusAuditCategory.Scrobble, type, status,
            AuditSubjectType.Series, seriesId: seriesId, payload: details, error: error, userId: userId, ct: ct);

    public Task LogChapterScrobbleAsync(KavitaPlusEventType type, int seriesId, int chapterId, AuditLogScrobbleParamsDto details,
        AuditStatus status, string? error = null, int? userId = null, CancellationToken ct = default) =>
        LogAsync(KavitaPlusAuditCategory.Scrobble, type, status,
            AuditSubjectType.Chapter, seriesId: seriesId, subjectId: chapterId, payload: details, error: error, userId: userId, ct: ct);

    public async Task PurgeOldLogsAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        await unitOfWork.KavitaPlusAuditRepository.DeleteOlderThanAsync(cutoff, ct);
        logger.LogInformation("[Kavita+ Audit] Purged audit logs older than {Cutoff:yyyy-MM-dd}", cutoff);
    }
}
