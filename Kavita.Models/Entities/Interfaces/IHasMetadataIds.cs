namespace Kavita.Models.Entities.Interfaces;
#nullable enable

/// <summary>
/// An entity has metadata markers
/// </summary>
public interface IHasMetadataIds
{
    public int AniListId { get; set; }
    /// <summary>
    /// https://myanimelist.net/store/manga/{MalId}/Blue_Lock
    /// </summary>
    public long MalId { get; set; }
    public int HardcoverId { get; set; }
    public long MetronId { get; set; }
    public string? ComicVineId { get; set; }
    public long MangaBakaId { get; set; }
    public int CbrId { get; set; }
}
