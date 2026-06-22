#nullable enable
using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus.Audit;

public sealed record AuditLogSystemTokenRefreshParamsDto
{
    public ScrobbleProvider Provider { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
}

public sealed record AuditLogSystemProviderInfoSyncParamsDto
{
    public ScrobbleProvider Provider { get; init; }
    public KavitaPlusUserInfo? UserInfo { get; init; }
}

