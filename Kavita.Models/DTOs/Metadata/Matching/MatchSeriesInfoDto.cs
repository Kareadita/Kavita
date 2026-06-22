using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.DTOs.Metadata.Matching;

public sealed record MatchSeriesInfoDto
{
    public bool HasMatch { get; set; }
    /// <summary>
    /// Dictates there is a Match AND it's AniList
    /// </summary>
    public bool IsLegacy { get; set; }
    public PlusMediaFormat PlusMediaFormat { get; set; }
    public LibraryType LibraryType { get; set; }
    /// <summary>
    /// Where the Match came from
    /// </summary>
    public MetadataProvider? MatchedProvider { get; set; }
    /// <summary>
    /// Who we would match against
    /// </summary>
    public MetadataProvider PrimaryProvider { get; set; }
    public MangaFormat SeriesFormat { get; set; }
    public int? MangaBakaId { get; set; }
    public int? AniListId { get; set; }
    public int? HardcoverId { get; set; }
    public int? CbrId { get; set; }
}
