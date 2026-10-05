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
    /// <summary>
    /// A folder that held files of this series was neither read nor skipped as unchanged, so some of its files are gone.
    /// The series must be processed even when no file was parsed for it
    /// </summary>
    public bool HasMissingFiles { get; set; }
    /// <summary>
    /// The series in the database this parsed series belongs to, when exactly one matches any of its names
    /// </summary>
    public int? ExistingSeriesId { get; set; }
}

public sealed record SeriesNameMatch(int Id, MangaFormat Format, string NormalizedName,
    string NormalizedLocalizedName, string NormalizedOriginalName);

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
    /// When unchanged, the series that have files covered by this folder. They get a placeholder each
    /// </summary>
    public IList<SeriesModified> UnchangedSeries { get; set; } = [];
    /// <summary>
    /// When unchanged, only files directly in the folder were checked. Subfolders are covered by their own results
    /// </summary>
    public bool IsShallow { get; set; }
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
    /// <inheritdoc cref="Kavita.Models.Parser.ParsedSeries.HasMissingFiles"/>
    public bool HasMissingFiles { get; set; }
}

public class SeriesModified
{
    public required string? FolderPath { get; set; }
    public required string? LowestFolderPath { get; set; }
    public required string SeriesName { get; set; }
    public DateTime LastScanned { get; set; }
    public MangaFormat Format { get; set; }
    public IEnumerable<string> LibraryRoots { get; set; } = ArraySegment<string>.Empty;
    /// <summary>
    /// The series' files, keyed by the folder that directly holds them
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<KnownFile>> FilesByFolder { get; set; } =
        new Dictionary<string, IReadOnlyList<KnownFile>>();
}
