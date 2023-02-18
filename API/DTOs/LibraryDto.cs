using System;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs;

public class LibraryDto
{
    public int Id { get; init; }
    public string Name { get; init; }
    /// <summary>
    /// Last time Library was scanned
    /// </summary>
    public DateTime LastScanned { get; init; }
    public LibraryType Type { get; init; }
    /// <summary>
    /// An optional Cover Image or null
    /// </summary>
    public string CoverImage { get; init; }
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
    /// Include library series in Search
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;
    public ICollection<string> Folders { get; init; }
}
