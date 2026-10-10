using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Models.Entities;
#nullable enable

/// <summary>
/// Represents issues found during scanning or interacting with media. For example) Can't open file, corrupt media, missing content in epub.
/// </summary>
public class MediaError : IEntityDate
{
    public int Id { get; set; }
    /// <summary>
    /// Format Type (RAR, ZIP, 7Zip, Epub, PDF)
    /// </summary>
    public required string Extension { get; set; }
    /// <summary>
    /// Full Filepath to the file that has some issue
    /// </summary>
    public required string FilePath { get; set; }
    /// <summary>
    /// Developer defined string
    /// </summary>
    public string Comment { get; set; }
    /// <summary>
    /// Exception message
    /// </summary>
    public string Details { get; set; }
    public long? Bytes { get; set; }
    /// <summary>
    /// Set when the scanner recorded the file. The scanner treats its rows as known files while the size and write time match
    /// </summary>
    public int? LibraryId { get; set; }
    public Library? Library { get; set; }
    public DateTime? FileLastWriteTimeUtc { get; set; }
    public int? SeriesId { get; set; }
    public Series? Series { get; set; }
    public MediaErrorProducer Producer { get; set; }
    public MediaErrorReason Reason { get; set; }
    /// <summary>
    /// Last time the file failed
    /// </summary>
    public DateTime LastSeenUtc { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
