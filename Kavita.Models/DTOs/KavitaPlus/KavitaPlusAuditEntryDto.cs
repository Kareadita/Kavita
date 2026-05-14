using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditEntryDto(
    long Id,
    DateTime CreatedUtc,
    KavitaPlusAuditCategory Category,
    KavitaPlusEventType EventType,
    AuditStatus Status,
    int? SeriesId,
    string? SeriesName,
    AuditSubjectType SubjectType,
    int? SubjectId,
    string? SubjectName,
    int? UserId,
    string? Username,
    IList<MetadataFieldChange>? Diff,
    string? ErrorMessage
);
