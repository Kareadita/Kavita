using System.Collections.Generic;
using API.DTOs.Scrobbling;

namespace API.DTOs.KavitaPlus.ExternalMetadata;

internal class MatchSeriesRequestDto
{
    public string SeriesName { get; set; }
    public ICollection<string> AlternativeNames { get; set; }
    public int Year { get; set; } = 0;
    public string Query { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public string? HardcoverId { get; set; }
    public PlusMediaFormat Format { get; set; }
}
