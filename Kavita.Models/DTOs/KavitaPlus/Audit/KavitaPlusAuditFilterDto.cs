using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditFilterDto
{
    public KavitaPlusAuditCategory? Category { get; init; }
    public AuditStatus? Status { get; init; }
    public AuditSubjectType? SubjectType { get; init; }
    /// <summary>
    /// When set, forces <see cref="Category"/> to be <see cref="KavitaPlusAuditCategory.Scrobble"/>
    /// </summary>
    public ScrobbleProvider? Provider { get; init; }
    public int? UserId { get; init; }
    public int? SeriesId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Search { get; init; }
}
