using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using Kavita.Models.Attributes;

namespace Kavita.Models.DTOs.Stats.V3;

public sealed record LibraryStatV3
{
    public bool IncludeInDashboard { get; set; }
    public bool IncludeInSearch { get; set; }
    [Obsolete("Use FolderWatchMode")]
    public bool UsingFolderWatching { get; set; }
    [EnumDataType(typeof(FolderWatchMode))]
    public FolderWatchMode FolderWatchMode { get; set; }
    /// <summary>
    /// Are any exclude patterns setup
    /// </summary>
    public bool UsingExcludePatterns { get; set; }
    /// <summary>
    /// Will this library create collections from ComicInfo
    /// </summary>
    public bool CreateCollectionsFromMetadata { get; set; }
    /// <summary>
    /// Will this library create reading lists from ComicInfo
    /// </summary>
    public bool CreateReadingListsFromMetadata { get; set; }
    /// <summary>
    /// If the library has metadata turned on
    /// </summary>
    public bool EnabledMetadata { get; set; }
    /// <summary>
    /// Type of the Library
    /// </summary>
    [EnumDataType(typeof(LibraryType))]
    public LibraryType LibraryType { get; set; }
    [EnumCollection(typeof(FileTypeGroup))]
    public ICollection<FileTypeGroup> FileTypes { get; set; }
    /// <summary>
    /// Last time library was fully scanned
    /// </summary>
    public DateTime LastScanned { get; set; }
    /// <summary>
    /// Number of folders the library has
    /// </summary>
    public int NumberOfFolders { get; set; }
    /// <summary>
    /// Duration of the last non-forced library scan, null when there was none since the upgrade
    /// </summary>
    public long? LastScanDurationMs { get; set; }
    /// <summary>
    /// Folders below the roots walked by the last non-forced library scan
    /// </summary>
    public int? LastScanFolderCount { get; set; }
    public long? LastScanListMs { get; set; }
    public long? LastScanWalkMs { get; set; }
    public long? LastScanParseMs { get; set; }
    /// <summary>
    /// Epub chapters made of more than one file
    /// </summary>
    /// <remarks>
    /// Counted to decide on https://github.com/Kareadita/Kavita/issues/5007: a non-forced scan does not recount
    /// words when one file of such a chapter is deleted
    /// </remarks>
    public int? MultiFileEpubChapterCount { get; set; }
}
