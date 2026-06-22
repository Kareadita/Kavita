namespace Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;

public record MetadataRequest
{
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public int? HardcoverId { get; set; }
    public int? CbrId { get; set; }
    public long? MangabakaId { get; set; }
    public string? GoogleBooksId { get; set; }
    public string? MangaDexId { get; set; }
    public long? MetronId { get; set; }
    public string? ComicVineId { get; set; }
    /// <summary>
    /// Is there only a single Chapter
    /// </summary>
    /// <remarks>This is important for Hardcover matching</remarks>
    public bool IsStandAlone { get; set; }
}
