using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums.Audit;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditEntryDto
{
    public long Id { get; init; }
    public DateTime CreatedUtc { get; init; }
    [EnumDataType(typeof(KavitaPlusAuditCategory))]
    public KavitaPlusAuditCategory Category { get; init; }
    [EnumDataType(typeof(KavitaPlusEventType))]
    public KavitaPlusEventType EventType { get; init; }
    [EnumDataType(typeof(AuditStatus))]
    public AuditStatus Status { get; init; }
    public int? SeriesId { get; init; }
    public int? LibraryId { get; init; }
    public string? SeriesName { get; init; }
    [EnumDataType(typeof(AuditSubjectType))]
    public AuditSubjectType SubjectType { get; init; }
    public int? SubjectId { get; init; }
    public int? UserId { get; init; }
    public string? Username { get; init; }
    public IList<MetadataFieldChangeDto>? Diff { get; init; }
    public string? ErrorMessage { get; init; }
    public int? ScrobbleErrorId { get; init; }
    public KavitaPlusScrobbleDetailsDto? ScrobbleDetails { get; init; }
    public KavitaPlusAuditMatchDetailsDto? MatchDetails { get; init; }
    public KavitaPlusAuditSyncDetailsDto? SyncDetails { get; init; }
    public KavitaPlusAuditMetadataExtrasDto? MetadataExtras { get; init; }
    public KavitaPlusAuditSystemDetailsDto? SystemDetails { get; init; }
    public bool CanRetry { get; init; }
}
