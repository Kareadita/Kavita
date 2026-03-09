using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Statistics;
#nullable enable

public sealed record FileExtensionDto
{
    public string? Extension { get; set; }
    public MangaFormat Format { get; set; }
    public long TotalSize { get; set; }
    public long TotalFiles { get; set; }
}

public sealed record FileExtensionBreakdownDto
{
    /// <summary>
    /// Total bytes for all files
    /// </summary>
    public long TotalFileSize { get; set; }

    public IList<FileExtensionDto> FileBreakdown { get; set; } = default!;

}
