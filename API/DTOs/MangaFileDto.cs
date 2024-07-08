using System;
using API.Entities.Enums;

namespace API.DTOs;

public class MangaFileDto
{
    public int Id { get; init; }
    /// <summary>
    /// Absolute path to the archive file (normalized)
    /// </summary>
    public string FilePath { get; init; } = default!;
    /// <summary>
    /// Number of pages for the given file
    /// </summary>
    public int Pages { get; init; }
    /// <summary>
    /// How many bytes make up this file
    /// </summary>
    public long Bytes { get; init; }
    public MangaFormat Format { get; init; }
    public DateTime Created { get; init; }
    /// <summary>
    /// File extension
    /// </summary>
    public string? Extension { get; set; }

}
