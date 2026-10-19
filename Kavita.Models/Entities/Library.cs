using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Common.Extensions;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities;

public class Library : IEntityDate, IHasCoverImage
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? CoverImage { get; set; }
    public string PrimaryColor { get; set; }
    public string SecondaryColor { get; set; }
    public LibraryType Type { get; set; }
    public FolderWatchMode FolderWatchMode { get; set; } = FolderWatchMode.Automatic;
    public LibraryFolderWatchState FolderWatchState { get; set; } = new();
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
    /// <summary>
    /// Should Kavita read metadata files from the library
    /// </summary>
    public bool EnableMetadata { get; set; } = true;
    /// <summary>
    /// Should Kavita remove sort articles "The" for the sort name
    /// </summary>
    public bool RemovePrefixForSortName { get; set; } = false;
    /// <summary>
    /// Should series inherit web links from the first chapter/volume
    /// </summary>
    public bool InheritWebLinksFromFirstChapter { get; set; } = false;
    /// <summary>
    /// Language to assign to series if none is set in the metadata
    /// </summary>
    public string DefaultLanguage { get; set; } = string.Empty;

    public MetadataProvider MetadataProvider { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    /// <summary>
    /// Last time Library was scanned
    /// </summary>
    /// <remarks>Time stored in UTC</remarks>
    public DateTime LastScanned { get; set; }
    public LibraryScanInfo ScanInfo { get; set; } = new();
    public ICollection<FolderPath> Folders { get; set; } = null!;
    public ICollection<AppUser> AppUsers { get; set; } = null!;
    public ICollection<Series> Series { get; set; } = null!;
    public ICollection<LibraryFileTypeGroup> LibraryFileTypes { get; set; } = new List<LibraryFileTypeGroup>();
    public ICollection<LibraryExcludePattern> LibraryExcludePatterns { get; set; } = new List<LibraryExcludePattern>();

    /// <summary>
    /// Adds and removes folder rows to match <paramref name="paths"/>, keeping the rows of folders still in it
    /// </summary>
    /// <returns>True when a folder was added or removed</returns>
    public bool SyncFolders(IEnumerable<string> paths)
    {
        var wanted = paths
            .DistinctBy(FolderKey)
            .ToDictionary(FolderKey);

        var removed = Folders.Where(f => !wanted.ContainsKey(FolderKey(f.Path))).ToList();
        foreach (var folder in removed)
        {
            Folders.Remove(folder);
        }

        var kept = Folders.Select(f => FolderKey(f.Path)).ToHashSet();
        var added = wanted
            .Where(w => !kept.Contains(w.Key))
            .Select(w => new FolderPath { Path = w.Value })
            .ToList();
        foreach (var folder in added)
        {
            Folders.Add(folder);
        }

        return removed.Count > 0 || added.Count > 0;
    }

    private static string FolderKey(string path) => path.NormalizePath().TrimEnd('/');

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
