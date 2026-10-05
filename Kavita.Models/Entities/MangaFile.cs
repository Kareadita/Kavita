
using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Models.Entities;

/// <summary>
/// Represents a wrapper to the underlying file. This provides information around file, like number of pages, format, etc.
/// </summary>
public class MangaFile : IEntityDate
{
    public int Id { get; set; }
    /// <summary>
    /// The filename without extension
    /// </summary>
    public string FileName { get; set; }
    /// <summary>
    /// Absolute path to the archive file
    /// </summary>
    public required string FilePath { get; set; }
    /// <summary>
    /// A hash of the document using Koreader's unique hashing algorithm
    /// </summary>
    public string? KoreaderHash { get; set; }
    /// <summary>
    /// Number of pages for the given file
    /// </summary>
    public int Pages { get; set; }
    public MangaFormat Format { get; set; }
    /// <summary>
    /// How many bytes make up this file
    /// </summary>
    public long Bytes { get; set; }
    /// <summary>
    /// File extension
    /// </summary>
    public string? Extension { get; set; }
    /// <inheritdoc cref="IEntityDate.Created"/>
    public DateTime Created { get; set; }
    /// <inheritdoc cref="IEntityDate.LastModified"/>
    /// <remarks>DataContext sets this to the save time. For the file's own write time see <see cref="FileLastWriteTimeUtc"/></remarks>
    public DateTime LastModified { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    /// <summary>
    /// The file's own write time when it was last read. Null until the first scan after this column was added
    /// </summary>
    /// <remarks>
    /// Not <see cref="LastModifiedUtc"/>: DataContext overwrites that with the save time.
    /// Added in v0.9.2. Once every install has scanned since then, the null fallback in the scanner can be removed
    /// </remarks>
    public DateTime? FileLastWriteTimeUtc { get; set; }

    /// <summary>
    /// Last time file analysis ran on this file
    /// </summary>
    public DateTime LastFileAnalysis { get; set; }
    public DateTime LastFileAnalysisUtc { get; set; }
    /// <summary>
    /// The file's write time when its words were last counted. Null until the first analysis after v0.9.2
    /// </summary>
    public DateTime? AnalyzedFileWriteTimeUtc { get; set; }


    // Relationship Mapping
    public Chapter Chapter { get; set; } = null!;
    public int ChapterId { get; set; }


    public void UpdateLastFileAnalysis()
    {
        LastFileAnalysis = DateTime.Now;
        LastFileAnalysisUtc = DateTime.UtcNow;
    }
}
