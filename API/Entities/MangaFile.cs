
using System;
using System.IO;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities;

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
    /// <summary>
    /// Last time underlying file was modified
    /// </summary>
    /// <remarks>This gets updated anytime the file is scanned</remarks>
    public DateTime LastModified { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    /// <summary>
    /// Last time file analysis ran on this file
    /// </summary>
    public DateTime LastFileAnalysis { get; set; }
    public DateTime LastFileAnalysisUtc { get; set; }


    // Relationship Mapping
    public Chapter Chapter { get; set; } = null!;
    public int ChapterId { get; set; }


    /// <summary>
    /// Updates the Last Modified time of the underlying file to the LastWriteTime
    /// </summary>
    public void UpdateLastModified()
    {
        if (FilePath == null) return;
        LastModified = File.GetLastWriteTime(FilePath);
        LastModifiedUtc = File.GetLastWriteTimeUtc(FilePath);
    }

    public void UpdateLastFileAnalysis()
    {
        LastFileAnalysis = DateTime.Now;
        LastFileAnalysisUtc = DateTime.UtcNow;
    }
}
