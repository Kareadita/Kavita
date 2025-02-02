using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;

namespace API.DTOs;

public class UpdateLibraryDto
{
    [Required]
    public int Id { get; init; }
    [Required]
    public required string Name { get; init; }
    [Required]
    public LibraryType Type { get; set; }
    [Required]
    public required IEnumerable<string> Folders { get; init; }
    [Required]
    public bool FolderWatching { get; init; }
    [Required]
    public bool IncludeInDashboard { get; init; }
    [Required]
    public bool IncludeInSearch { get; init; }
    [Required]
    public bool ManageCollections { get; init; }
    [Required]
    public bool ManageReadingLists { get; init; }
    [Required]
    public bool AllowScrobbling { get; init; }
    [Required]
    public bool AllowMetadataMatching { get; init; }
    /// <summary>
    /// What types of files to allow the scanner to pickup
    /// </summary>
    [Required]
    public ICollection<FileTypeGroup> FileGroupTypes { get; init; }
    /// <summary>
    /// A set of Glob patterns that the scanner will exclude processing
    /// </summary>
    [Required]
    public ICollection<string> ExcludePatterns { get; init; }
}
