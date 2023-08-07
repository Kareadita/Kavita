using System;
using API.Entities.Interfaces;

namespace API.Entities;

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
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
