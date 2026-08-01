#nullable enable
using System;
using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus.Audit;

public sealed record AuditLogSystemTokenRefreshParamsDto
{
    [EnumDataType(typeof(ScrobbleProvider))]
    public ScrobbleProvider Provider { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
}

public sealed record AuditLogSystemProviderInfoSyncParamsDto
{
    [EnumDataType(typeof(ScrobbleProvider))]
    public ScrobbleProvider Provider { get; init; }
    public KavitaPlusUserInfo? UserInfo { get; init; }
}
