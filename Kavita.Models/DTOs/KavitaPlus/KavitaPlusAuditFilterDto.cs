using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditFilterDto(
    KavitaPlusAuditCategory? Category = null,
    AuditStatus? Status = null,
    AuditSubjectType? SubjectType = null,
    int? UserId = null,
    int? SeriesId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null
);
