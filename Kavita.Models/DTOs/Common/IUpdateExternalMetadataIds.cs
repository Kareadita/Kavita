namespace Kavita.Models.DTOs.Common;
#nullable enable

/// <summary>
/// Provides a set of optional (non-API breaking) fields for updating external metadata ids
/// </summary>
public interface IUpdateExternalMetadataIds
{
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public int? HardcoverId { get; set; }
    public long? MetronId { get; set; }
    public string? ComicVineId { get; set; }
    public long? MangaBakaId { get; set; }
    public int? CbrId { get; set; }
}
