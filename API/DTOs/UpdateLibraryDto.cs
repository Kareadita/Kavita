using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities;
using API.Entities.Enums;

namespace API.DTOs;

public sealed record UpdateLibraryDto
{
    [Required]
    public int Id { get; init; }
    /// <inheritdoc cref="Library.Name"/>
    [Required]
    public required string Name { get; init; }
    /// <inheritdoc cref="Library.Type"/>
    [Required]
    public LibraryType Type { get; set; }
    /// <inheritdoc cref="Library.Folders"/>
    [Required]
    public required IEnumerable<string> Folders { get; init; }
    /// <inheritdoc cref="Library.FolderWatching"/>
    [Required]
    public bool FolderWatching { get; init; }
    /// <inheritdoc cref="Library.IncludeInDashboard"/>
    [Required]
    public bool IncludeInDashboard { get; init; }
    /// <inheritdoc cref="Library.IncludeInSearch"/>
    [Required]
    public bool IncludeInSearch { get; init; }
    /// <inheritdoc cref="Library.ManageCollections"/>
    [Required]
    public bool ManageCollections { get; init; }
    /// <inheritdoc cref="Library.ManageReadingLists"/>
    [Required]
    public bool ManageReadingLists { get; init; }
    /// <inheritdoc cref="Library.AllowScrobbling"/>
    [Required]
    public bool AllowScrobbling { get; init; }
    /// <inheritdoc cref="Library.AllowMetadataMatching"/>
    [Required]
    public bool AllowMetadataMatching { get; init; }
    /// <inheritdoc cref="Library.EnableMetadata"/>
    [Required]
    public bool EnableMetadata { get; init; }
    /// <inheritdoc cref="Library.RemovePrefixForSortName"/>
    [Required]
    public bool RemovePrefixForSortName { get; init; }
    /// <inheritdoc cref="Library.InheritWebLinksFromFirstChapter"/>
    [Required]
    public bool InheritWebLinksFromFirstChapter { get; init; }
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
