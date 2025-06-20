using System.Collections.Generic;
using API.DTOs.Scrobbling;

namespace API.DTOs.KavitaPlus.ExternalMetadata;
#nullable enable

/// <summary>
/// Represents a request to match some series from Kavita to an external id which K+ uses.
/// </summary>
public sealed record MatchSeriesRequestDto
{
    public required string SeriesName { get; set; }
    public ICollection<string> AlternativeNames { get; set; } = [];
    public int Year { get; set; } = 0;
    public string? Query { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public string? HardcoverId { get; set; }
    public int? CbrId { get; set; }
    public PlusMediaFormat Format { get; set; }
}
