using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs;
#nullable enable

public sealed record UpdatePersonDto
{
    [Required]
    public int Id { get; init; }
    [Required]
    public bool CoverImageLocked { get; set; }
    [Required]
    public string Name {get; set;}
    public IList<string> Aliases { get; set; } = [];
    public string? Description { get; set; }

    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public string? HardcoverId { get; set; }
    public string? Asin { get; set; }
}
