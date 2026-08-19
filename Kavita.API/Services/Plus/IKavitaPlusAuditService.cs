using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.History;
using Kavita.Models.Entities.Scrobble;

namespace Kavita.API.Services.Plus;

public interface IKavitaPlusAuditService
{
    Task LogAsync(
        KavitaPlusAuditCategory category,
        KavitaPlusEventType eventType,
        AuditStatus status,
        AuditSubjectType subjectType = AuditSubjectType.Global,
        int? seriesId = null,
        int? subjectId = null,
        object? payload = null,
        string? error = null,
        int? userId = null,
        ScrobbleError? scrobbleError = null,
        CancellationToken ct = default);

    /// <summary>
    /// Logs an audit log only if the same audit log hasn't been logged in the last 24h
    /// Audit logs are the same if their <see cref="KavitaPlusAuditLog.Category"/>, <see cref="KavitaPlusAuditLog.EventType"/>,
    /// <see cref="KavitaPlusAuditLog.Status"/>, <see cref="KavitaPlusAuditLog.SubjectType"/>, <see cref="KavitaPlusAuditLog.ErrorMessage"/>,
    /// and the <see cref="idSelector"/> are the same.
    /// </summary>
    Task LogTemperedAsync(
        Expression<Func<KavitaPlusAuditLog, bool>> idSelector,
        KavitaPlusAuditCategory category,
        KavitaPlusEventType eventType,
        AuditStatus status,
        AuditSubjectType subjectType = AuditSubjectType.Global,
        int? seriesId = null,
        int? subjectId = null,
        object? payload = null,
        string? error = null,
        int? userId = null,
        CancellationToken ct = default);

    Task LogMatchAsync(KavitaPlusEventType type, int seriesId, object payload,
        AuditStatus status = AuditStatus.Success, string? error = null, CancellationToken ct = default);

    Task LogMetadataAsync(int seriesId, IList<MetadataFieldChangeDto> changes, CancellationToken ct = default);

    Task LogChapterMetadataAsync(int chapterId, int seriesId, IList<MetadataFieldChangeDto> changes,
        CancellationToken ct = default);

    Task LogPersonAsync(KavitaPlusEventType type, int personId, object payload,
        AuditStatus status = AuditStatus.Success, CancellationToken ct = default);

    Task LogCollectionAsync(KavitaPlusEventType type, int collectionId, object payload,
        AuditStatus status = AuditStatus.Success, int? userId = null, CancellationToken ct = default);

    Task LogScrobbleAsync(KavitaPlusEventType type, int seriesId, AuditLogScrobbleParamsDto details,
        AuditStatus status, string? error = null, int? userId = null, ScrobbleError? scrobbleError = null, CancellationToken ct = default);

    Task LogChapterScrobbleAsync(KavitaPlusEventType type, int seriesId, int chapterId, AuditLogScrobbleParamsDto details,
        AuditStatus status, string? error = null, int? userId = null, ScrobbleError? scrobbleError = null, CancellationToken ct = default);

    Task PurgeOldLogsAsync(CancellationToken ct = default);
}
