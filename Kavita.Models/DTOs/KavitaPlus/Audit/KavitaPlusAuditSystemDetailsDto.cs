using System;
using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus;

public sealed record KavitaPlusAuditSystemDetailsDto
{

    public ScrobbleProvider Provider { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public KavitaPlusUserInfo? UserInfo { get; init; }

    public static KavitaPlusAuditSystemDetailsDto From(AuditLogSystemTokenRefreshParamsDto dto)
    {
        return new KavitaPlusAuditSystemDetailsDto
        {
            Provider = dto.Provider,
            ValidUntilUtc = dto.ValidUntilUtc,
        };
    }

    public static KavitaPlusAuditSystemDetailsDto From(AuditLogSystemProviderInfoSyncParamsDto dto)
    {
        return new KavitaPlusAuditSystemDetailsDto
        {
            Provider = dto.Provider,
            UserInfo = dto.UserInfo,
        };
    }

}
