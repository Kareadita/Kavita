using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using System.ComponentModel.DataAnnotations;

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

public sealed record AuditLogMatchProviderOverrideParamsDto
{
    public string SeriesName { get; init; } = string.Empty;
    /// <summary>
    /// The provider that was matched against before the change
    /// </summary>
    [EnumDataType(typeof(MetadataProvider))]
    public MetadataProvider PreviousProvider { get; init; }
    /// <summary>
    /// The provider that will be matched against going forward
    /// </summary>
    [EnumDataType(typeof(MetadataProvider))]
    public MetadataProvider NewProvider { get; init; }
    /// <summary>
    /// False when the Series fell back to its Library's default provider
    /// </summary>
    public bool IsOverride { get; init; }
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
    public string MangaBakaEditionId { get; set; }
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
    [EnumDataType(typeof(MangaFormat))]
    public MangaFormat Format { get; init; }
    public long MangaBakaId { get; init; }
    public int CbrId { get; init; }
    public int AniListId { get; init; }
    public int HardcoverId { get; init; }
    [EnumDataType(typeof(MetadataFetchTrigger))]
    public MetadataFetchTrigger Trigger { get; init; }
}