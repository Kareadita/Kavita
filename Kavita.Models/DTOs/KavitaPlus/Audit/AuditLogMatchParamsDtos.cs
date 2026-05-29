using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;

namespace Kavita.Models.DTOs.KavitaPlus.Audit;
#nullable enable

public sealed record AuditLogMatchClearedParamsDto
{
    public string SeriesName { get; init; } = string.Empty;
    public string? MatchedName { get; init; }
}

public sealed record AuditLogMatchDontMatchParamsDto
{
    public string SeriesName { get; init; } = string.Empty;
    public bool DontMatch { get; init; }
}

public sealed record AuditLogMatchFailureParamsDto
{
    public string SeriesName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed record AuditLogMatchExternalIdsParamsDto
{
    public int AniListId { get; init; }
    public long MalId { get; init; }
    public long MangaBakaId { get; init; }
    public int CbrId { get; init; }
    public int HardcoverId { get; init; }
}

public sealed record AuditLogMatchedParamsDto
{
    public string SeriesName { get; init; } = string.Empty;
    public AuditLogMatchExternalIdsParamsDto Before { get; init; } = new();
    public AuditLogMatchExternalIdsParamsDto After { get; init; } = new();
    public string? MatchedName { get; init; }
}

public sealed record AuditLogMetadataFetchParamsDto
{
    public int SeriesId { get; init; }
    public int? LibraryId { get; init; }
    public MangaFormat Format { get; init; }
    public long MangaBakaId { get; init; }
    public int CbrId { get; init; }
    public int AniListId { get; init; }
    public int HardcoverId { get; init; }
    public MetadataFetchTrigger Trigger { get; init; }
}
