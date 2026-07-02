using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums.Audit;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditEntryDto
{
    public long Id { get; init; }
    public DateTime CreatedUtc { get; init; }
    public KavitaPlusAuditCategory Category { get; init; }
    public KavitaPlusEventType EventType { get; init; }
    public AuditStatus Status { get; init; }
    public int? SeriesId { get; init; }
    public int? LibraryId { get; init; }
    public string? SeriesName { get; init; }
    public AuditSubjectType SubjectType { get; init; }
    public int? SubjectId { get; init; }
    public int? UserId { get; init; }
    public string? Username { get; init; }
    public IList<MetadataFieldChangeDto>? Diff { get; init; }
    public string? ErrorMessage { get; init; }
    public KavitaPlusScrobbleDetailsDto? ScrobbleDetails { get; init; }
    public KavitaPlusAuditMatchDetailsDto? MatchDetails { get; init; }
    public KavitaPlusAuditSyncDetailsDto? SyncDetails { get; init; }
    public KavitaPlusAuditMetadataExtrasDto? MetadataExtras { get; init; }
    public KavitaPlusAuditSystemDetailsDto? SystemDetails { get; init; }
    public bool CanRetry { get; init; }
}
