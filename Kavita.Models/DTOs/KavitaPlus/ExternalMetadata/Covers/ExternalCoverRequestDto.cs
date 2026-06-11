using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Models.DTOs.KavitaPlus.ExternalMetadata.Covers;
#nullable enable

/// <summary>
/// Requests for Cover Images from Kavita+
/// </summary>
public sealed record ExternalCoverRequestDto
{
    public string? SeriesName { get; set; }
    public string? AltSeriesName { get; set; }

    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public long? MetronId { get; set; }
    public string? ComicVineId { get; set; }
    public int? MangabakaId { get; set; }
    public long? HardcoverId { get; set; }
    public int? CbrId { get; set; }
    public bool IsStandAlone { get; set; }

    public PlusMediaFormat MediaFormat { get; set; }

    /// <summary>When true, only volume/volume_back type images are returned.</summary>
    public bool VolumesOnly { get; set; }
    public bool ChaptersOnly { get; set; }
    /// <summary>When set, restrict results to this specific volume number.</summary>
    public float? VolumeNumber { get; set; }
    /// <summary>When set, restrict results to this specific chapter/issue number.</summary>
    public float? ChapterNumber { get; set; }
}
