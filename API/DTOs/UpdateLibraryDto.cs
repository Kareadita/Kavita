using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;

namespace API.DTOs;

public class UpdateLibraryDto
{
    [Required]
    public int Id { get; init; }
    [Required]
    public string Name { get; init; }
    [Required]
    public LibraryType Type { get; set; }
    [Required]
    public IEnumerable<string> Folders { get; init; }
    [Required]
    public bool FolderWatching { get; init; }
    [Required]
    public bool IncludeInDashboard { get; init; }
    [Required]
    public bool IncludeInRecommended { get; init; }
    [Required]
    public bool IncludeInSearch { get; init; }
    [Required]
    public bool CreateCollections { get; init; }

}
