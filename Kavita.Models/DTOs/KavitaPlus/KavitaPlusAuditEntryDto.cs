using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

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
    public string? SeriesName { get; init; }
    public AuditSubjectType SubjectType { get; init; }
    public int? SubjectId { get; init; }
    public string? SubjectName { get; init; }
    public int? UserId { get; init; }
    public string? Username { get; init; }
    public IList<MetadataFieldChange>? Diff { get; init; }
    public string? ErrorMessage { get; init; }
    public KavitaPlusScrobbleDetailsDto? ScrobbleDetails { get; init; }
}
