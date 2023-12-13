using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Statistics;
#nullable enable

public class FileExtensionDto
{
    public string? Extension { get; set; }
    public MangaFormat Format { get; set; }
    public long TotalSize { get; set; }
    public long TotalFiles { get; set; }
}

public class FileExtensionBreakdownDto
{
    /// <summary>
    /// Total bytes for all files
    /// </summary>
    public long TotalFileSize { get; set; }

    public IList<FileExtensionDto> FileBreakdown { get; set; } = default!;

}
