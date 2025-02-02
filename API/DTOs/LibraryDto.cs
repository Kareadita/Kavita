using System;
using System.Collections;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs;
#nullable enable

public class LibraryDto
{
    public int Id { get; init; }
    public string? Name { get; init; }
    /// <summary>
    /// Last time Library was scanned
    /// </summary>
    public DateTime LastScanned { get; init; }
    public LibraryType Type { get; init; }
    /// <summary>
    /// An optional Cover Image or null
    /// </summary>
    public string? CoverImage { get; init; }
    /// <summary>
    /// If Folder Watching is enabled for this library
    /// </summary>
    public bool FolderWatching { get; set; } = true;
    /// <summary>
    /// Include Library series on Dashboard Streams
    /// </summary>
    public bool IncludeInDashboard { get; set; } = true;
    /// <summary>
    /// Include Library series on Recommended Streams
    /// </summary>
    public bool IncludeInRecommended { get; set; } = true;
    /// <summary>
    /// Should this library create and manage collections from Metadata
    /// </summary>
    public bool ManageCollections { get; set; } = true;
    /// <summary>
    /// Should this library create and manage reading lists from Metadata
    /// </summary>
    public bool ManageReadingLists { get; set; } = true;
    /// <summary>
    /// Include library series in Search
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;
    /// <summary>
    /// Should this library allow Scrobble events to emit from it
    /// </summary>
    /// <remarks>Scrobbling requires a valid LicenseKey</remarks>
    public bool AllowScrobbling { get; set; } = true;
    public ICollection<string> Folders { get; init; } = new List<string>();
    /// <summary>
    /// When showing series, only parent series or series with no relationships will be returned
    /// </summary>
    public bool CollapseSeriesRelationships { get; set; } = false;
    /// <summary>
    /// The types of file type groups the library will scan for
    /// </summary>
    public ICollection<FileTypeGroup> LibraryFileTypes { get; set; }
    /// <summary>
    /// A set of globs that will exclude matching content from being scanned
    /// </summary>
    public ICollection<string> ExcludePatterns { get; set; }
    /// <summary>
    /// Allow any series within this Library to download metadata.
    /// </summary>
    /// <remarks>This does not exclude the library from being linked to wrt Series Relationships</remarks>
    /// <remarks>Requires a valid LicenseKey</remarks>
    public bool AllowMetadataMatching { get; set; } = true;
}
