using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Metadata.Matching;

public sealed record MatchSeriesInfoDto
{
    public bool HasMatch { get; set; }
    /// <summary>
    /// Dictates there is a Match AND it's AniList
    /// </summary>
    public bool IsLegacy { get; set; }
    [EnumDataType(typeof(PlusMediaFormat))]
    public PlusMediaFormat PlusMediaFormat { get; set; }
    [EnumDataType(typeof(LibraryType))]
    public LibraryType LibraryType { get; set; }
    [EnumDataType(typeof(MetadataProvider))]
    public MetadataProvider MetadataProvider { get; set; }
    [EnumDataType(typeof(MangaFormat))]
    public MangaFormat SeriesFormat { get; set; }
    public int? MangaBakaId { get; set; }
    public int? AniListId { get; set; }
    public int? HardcoverId { get; set; }
    public int? CbrId { get; set; }
    /// <summary>
    /// The currently selected MangaBaka edition, if any
    /// </summary>
    public string MangaBakaEditionId { get; set; } = string.Empty;
    public bool IsStandalone { get; set; }
}
