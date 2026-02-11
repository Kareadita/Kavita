using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Parser;

public class ParsedSeries
{
    /// <summary>
    /// Name of the Series
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Normalized Name of the Series
    /// </summary>
    public required string NormalizedName { get; init; }
    /// <summary>
    /// Format of the Series
    /// </summary>
    public required MangaFormat Format { get; init; }
    /// <summary>
    /// Has this Series changed or not aka do we need to process it or not.
    /// </summary>
    public bool HasChanged { get; set; }
}

public class ScanResult
{
    /// <summary>
    /// A list of files in the Folder. Empty if HasChanged = false
    /// </summary>
    public IList<string> Files { get; set; }
    /// <summary>
    /// A nested folder from Library Root (at any level)
    /// </summary>
    public string Folder { get; set; }
    /// <summary>
    /// The library root
    /// </summary>
    public string LibraryRoot { get; set; }
    /// <summary>
    /// Was the Folder scanned or not. If not modified since last scan, this will be false and Files empty
    /// </summary>
    public bool HasChanged { get; set; }
    /// <summary>
    /// Set in Stage 2: Parsed Info from the Files
    /// </summary>
    public IList<ParserInfo> ParserInfos { get; set; }
}

/// <summary>
/// The final product of ParseScannedFiles. This has all the processed parserInfo and is ready for tracking/processing into entities
/// </summary>
public class ScannedSeriesResult
{
    /// <summary>
    /// Was the Folder scanned or not. If not modified since last scan, this will be false and indicates that upstream should count this as skipped
    /// </summary>
    public bool HasChanged { get; set; }
    /// <summary>
    /// The Parsed Series information used for tracking
    /// </summary>
    public ParsedSeries ParsedSeries { get; set; }
    /// <summary>
    /// Parsed files
    /// </summary>
    public IList<ParserInfo> ParsedInfos { get; set; }
}

public class SeriesModified
{
    public required string? FolderPath { get; set; }
    public required string? LowestFolderPath { get; set; }
    public required string SeriesName { get; set; }
    public DateTime LastScanned { get; set; }
    public MangaFormat Format { get; set; }
    public IEnumerable<string> LibraryRoots { get; set; } = ArraySegment<string>.Empty;
}
