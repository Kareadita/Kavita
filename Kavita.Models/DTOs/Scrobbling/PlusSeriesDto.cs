using Kavita.Models.Entities.Enums.KavitaPlus;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Scrobbling;
#nullable enable

/// <summary>
/// Represents information about a potential Series for Kavita+
/// </summary>
public class PlusSeriesRequestDto
{
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public string? GoogleBooksId { get; set; }
    public string? MangaDexId { get; set; }
    public int? MangabakaId { get; set; }
    public int? HardcoverId { get; set; }
    /// <summary>
    /// ComicBookRoundup Id
    /// </summary>
    public int? CbrId { get; set; }
    public string SeriesName { get; set; }
    public string? AltSeriesName { get; set; }
    [EnumDataType(typeof(PlusMediaFormat))]
    public PlusMediaFormat MediaFormat { get; set; }
    /// <summary>
    /// Optional but can help with matching
    /// </summary>
    public int? ChapterCount { get; set; }
    /// <summary>
    /// Optional but can help with matching
    /// </summary>
    public int? VolumeCount { get; set; }
    public int? Year { get; set; }
}