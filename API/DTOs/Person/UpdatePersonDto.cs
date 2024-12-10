using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class UpdatePersonDto
{
    [Required]
    public int Id { get; init; }
    [Required]
    public bool CoverImageLocked { get; set; }
    [Required]
    public string Name {get; set;}
    public string? Description { get; set; }

    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public string? HardcoverId { get; set; }
    public string? Asin { get; set; }
}
