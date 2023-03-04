﻿using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Entities.Metadata;

namespace API.Entities;

public class Series : IEntityDate, IHasReadTimeEstimate
{
    public int Id { get; set; }
    /// <summary>
    /// The UI visible Name of the Series. This may or may not be the same as the OriginalName
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// Used internally for name matching. <see cref="Services.Tasks.Scanner.Parser.Parser.Normalize"/>
    /// </summary>
    public required string NormalizedName { get; set; }
    /// <summary>
    /// Used internally for localized name matching. <see cref="Services.Tasks.Scanner.Parser.Parser.Normalize"/>
    /// </summary>
    public required string NormalizedLocalizedName { get; set; }
    /// <summary>
    /// The name used to sort the Series. By default, will be the same as Name.
    /// </summary>
    public required string SortName { get; set; }
    /// <summary>
    /// Name in original language (Japanese for Manga). By default, will be same as Name.
    /// </summary>
    public required string LocalizedName { get; set; }
    /// <summary>
    /// Original Name on disk. Not exposed to UI.
    /// </summary>
    public required string OriginalName { get; set; }
    /// <summary>
    /// Time of creation
    /// </summary>
    public DateTime Created { get; set; }
    /// <summary>
    /// Whenever a modification occurs. Ie) New volumes, removed volumes, title update, etc
    /// </summary>
    public DateTime LastModified { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    /// <summary>
    /// Absolute path to the (managed) image file
    /// </summary>
    /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
    public string? CoverImage { get; set; }
    /// <summary>
    /// Denotes if the CoverImage has been overridden by the user. If so, it will not be updated during normal scan operations.
    /// </summary>
    public bool CoverImageLocked { get; set; }
    /// <summary>
    /// Sum of all Volume page counts
    /// </summary>
    public int Pages { get; set; }
    /// <summary>
    /// Highest path (that is under library root) that contains the series.
    /// </summary>
    /// <remarks><see cref="Services.Tasks.Scanner.Parser.Parser.NormalizePath"/> must be used before setting</remarks>
    public string? FolderPath { get; set; }
    /// <summary>
    /// Last time the folder was scanned
    /// </summary>
    public DateTime LastFolderScanned { get; set; }
    /// <summary>
    /// Last time the folder was scanned in Utc
    /// </summary>
    public DateTime LastFolderScannedUtc { get; set; }
    /// <summary>
    /// The type of all the files attached to this series
    /// </summary>
    public MangaFormat Format { get; set; } = MangaFormat.Unknown;

    public bool NameLocked { get; set; }
    public bool SortNameLocked { get; set; }
    public bool LocalizedNameLocked { get; set; }

    /// <summary>
    /// When a Chapter was last added onto the Series
    /// </summary>
    public DateTime LastChapterAdded { get; set; }
    public DateTime LastChapterAddedUtc { get; set; }

    /// <summary>
    /// Total Word count of all chapters in this chapter.
    /// </summary>
    /// <remarks>Word Count is only available from EPUB files</remarks>
    public long WordCount { get; set; }

    public int MinHoursToRead { get; set; }
    public int MaxHoursToRead { get; set; }
    public int AvgHoursToRead { get; set; }

    public SeriesMetadata Metadata { get; set; } = null!;

    public ICollection<AppUserRating> Ratings { get; set; } = null!;
    public ICollection<AppUserProgress> Progress { get; set; } = null!;

    /// <summary>
    /// Relations to other Series, like Sequels, Prequels, etc
    /// </summary>
    /// <remarks>1 to Many relationship</remarks>
    public ICollection<SeriesRelation> Relations { get; set; } = null!;
    public ICollection<SeriesRelation> RelationOf { get; set; } = null!;


    // Relationships
    public List<Volume> Volumes { get; set; } = null!;
    public Library Library { get; set; } = null!;
    public int LibraryId { get; set; }

    public void UpdateLastFolderScanned()
    {
        LastFolderScanned = DateTime.Now;
        LastFolderScannedUtc = DateTime.UtcNow;
    }

    public void UpdateLastChapterAdded()
    {
        LastChapterAdded = DateTime.Now;
        LastChapterAddedUtc = DateTime.UtcNow;
    }
}
