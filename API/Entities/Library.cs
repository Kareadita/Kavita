using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities;

public class Library : IEntityDate, IHasCoverImage
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? CoverImage { get; set; }
    public string PrimaryColor { get; set; }
    public string SecondaryColor { get; set; }
    public LibraryType Type { get; set; }
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
    /// Include library series in Search
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;
    /// <summary>
    /// Should this library create collections from Metadata
    /// </summary>
    public bool ManageCollections { get; set; } = true;
    /// <summary>
    /// Should this library create reading lists from Metadata
    /// </summary>
    public bool ManageReadingLists { get; set; } = true;
    /// <summary>
    /// Should this library allow Scrobble events to emit from it
    /// </summary>
    /// <remarks>Requires a valid LicenseKey</remarks>
    public bool AllowScrobbling { get; set; } = true;
    /// <summary>
    /// Allow any series within this Library to download metadata.
    /// </summary>
    /// <remarks>This does not exclude the library from being linked to wrt Series Relationships</remarks>
    /// <remarks>Requires a valid LicenseKey</remarks>
    public bool AllowMetadataMatching { get; set; } = true;


    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    /// <summary>
    /// Last time Library was scanned
    /// </summary>
    /// <remarks>Time stored in UTC</remarks>
    public DateTime LastScanned { get; set; }
    public ICollection<FolderPath> Folders { get; set; } = null!;
    public ICollection<AppUser> AppUsers { get; set; } = null!;
    public ICollection<Series> Series { get; set; } = null!;
    public ICollection<LibraryFileTypeGroup> LibraryFileTypes { get; set; } = new List<LibraryFileTypeGroup>();
    public ICollection<LibraryExcludePattern> LibraryExcludePatterns { get; set; } = new List<LibraryExcludePattern>();

    public void UpdateLastModified()
    {
        LastModified = DateTime.Now;
        LastModifiedUtc = DateTime.UtcNow;
    }

    public void UpdateLastScanned(DateTime? time)
    {
        if (time == null)
        {
            LastScanned = DateTime.Now;
        }
        else
        {
            LastScanned = (DateTime) time;
        }
    }

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
