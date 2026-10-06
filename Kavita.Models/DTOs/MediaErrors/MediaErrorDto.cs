using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.MediaErrors;
#nullable enable

public sealed record MediaErrorDto
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
    /// Exception message
    /// </summary>
    public string Details { get; set; }
    public MediaErrorProducer Producer { get; set; }
    public MediaErrorReason Reason { get; set; }
    public int? SeriesId { get; set; }
    public string? SeriesName { get; set; }
    public int? LibraryId { get; set; }
    public string? LibraryName { get; set; }
    public bool IsDismissed { get; set; }
    /// <summary>
    /// Last time the file failed
    /// </summary>
    public DateTime LastSeenUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}
