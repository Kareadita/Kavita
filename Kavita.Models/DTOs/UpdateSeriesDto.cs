using Kavita.Models.DTOs.Common;

namespace Kavita.Models.DTOs;
#nullable enable

public sealed record UpdateSeriesDto : IUpdateExternalMetadataIds
{
    public int Id { get; init; }
    public string? LocalizedName { get; init; }
    public string? SortName { get; init; }
    public bool CoverImageLocked { get; set; }

    public bool SortNameLocked { get; set; }
    public bool LocalizedNameLocked { get; set; }

    #region External Metadata Ids
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public int? HardcoverId { get; set; }
    public long? MetronId { get; set; }
    public string? ComicVineId { get; set; }
    public long? MangaBakaId { get; set; }
    public int? CbrId { get; set; }
    #endregion
}
