using API.DTOs.Scrobbling;

namespace API.DTOs.KavitaPlus.ExternalMetadata;

/// <summary>
/// Used for matching and fetching metadata on a series
/// </summary>
internal class ExternalMetadataIdsDto
{
    public long? MalId { get; set; }
    public int? AniListId { get; set; }

    public string? SeriesName { get; set; }
    public string? LocalizedSeriesName { get; set; }
    public PlusMediaFormat? PlusMediaFormat { get; set; } = DTOs.Scrobbling.PlusMediaFormat.Unknown;
}
