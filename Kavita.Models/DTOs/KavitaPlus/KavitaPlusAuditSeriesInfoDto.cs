using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.Common;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditSeriesInfoDto : IUpdateExternalMetadataIds
{
    public int SeriesId { get; init; }
    public int LibraryId { get; init; }
    public string SeriesName { get; init; } = string.Empty;
    public bool IsMatched { get; init; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public int? HardcoverId { get; set; }
    public long? MetronId { get; set; }
    public string? ComicVineId { get; set; }
    public long? MangaBakaId { get; set; }
    public int? CbrId { get; set; }
    public MetadataProvider? MetadataProvider { get; set; }
    public DateTime? NextRefreshUtc { get; init; }
    public DateTime? LastRefreshedUtc { get; init; }
    public IList<KavitaPlusAuditEntryDto> RecentEvents { get; init; } = [];
}
