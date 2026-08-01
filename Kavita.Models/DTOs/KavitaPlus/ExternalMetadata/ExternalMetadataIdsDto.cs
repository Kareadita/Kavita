using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums.KavitaPlus;
using System.ComponentModel.DataAnnotations;

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
    public string? MangaBakaEditionId { get; set; }
    public int? HardcoverId { get; set; }
    /// <summary>
    /// If the series should be considered a standalone book. This is currently only used for Hardcover.
    /// If true, the associated id will point towards a book rather than a series
    /// </summary>
    public bool IsStandAlone { get; set; }
    public int? CbrId { get; set; }

    public string? SeriesName { get; set; }
    public string? LocalizedSeriesName { get; set; }
    [EnumDataType(typeof(PlusMediaFormat))]
    public PlusMediaFormat? PlusMediaFormat { get; set; } = Kavita.Models.Entities.Enums.KavitaPlus.PlusMediaFormat.Unknown;
}