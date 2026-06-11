using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
#nullable enable

/// <summary>
/// Used for matching and fetching metadata on a series
/// </summary>
public sealed record ExternalMetadataIdsDto
{
    public long? MalId { get; set; }
    public int? AniListId { get; set; }
    public int? MangabakaId { get; set; }
    public int? HardcoverId { get; set; }
    public int? CbrId { get; set; }

    public string? SeriesName { get; set; }
    public string? LocalizedSeriesName { get; set; }
    public PlusMediaFormat? PlusMediaFormat { get; set; } = Kavita.Models.Entities.Enums.KavitaPlus.PlusMediaFormat.Unknown;
}
