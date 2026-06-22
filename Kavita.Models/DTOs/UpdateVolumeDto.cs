using Kavita.Models.DTOs.Common;

namespace Kavita.Models.DTOs;

public sealed record UpdateVolumeDto : IUpdateExternalMetadataIds
{
    public int Id { get; init; }

    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public int? HardcoverId { get; set; }
    public long? MetronId { get; set; }
    public string ComicVineId { get; set; }
    public long? MangaBakaId { get; set; }
    public int? CbrId { get; set; }
}
