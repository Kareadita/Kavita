using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditFilterDto
{
    [EnumDataType(typeof(KavitaPlusAuditCategory))]
    public KavitaPlusAuditCategory? Category { get; init; }
    [EnumDataType(typeof(AuditStatus))]
    public AuditStatus? Status { get; init; }
    [EnumDataType(typeof(AuditSubjectType))]
    public AuditSubjectType? SubjectType { get; init; }
    /// <summary>
    /// When set, forces <see cref="Category"/> to be <see cref="KavitaPlusAuditCategory.Scrobble"/>
    /// </summary>
    [EnumDataType(typeof(ScrobbleProvider))]
    public ScrobbleProvider? Provider { get; init; }
    public int? UserId { get; init; }
    public int? SeriesId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Search { get; init; }
}