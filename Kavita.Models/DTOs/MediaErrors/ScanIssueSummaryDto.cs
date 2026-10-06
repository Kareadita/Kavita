using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.MediaErrors;
#nullable enable

/// <summary>
/// Files in a library the scanner could not read, dismissed ones left out
/// </summary>
/// <param name="Count">Files in the library that could not be read</param>
/// <param name="NewCount">Files that failed for the first time, or for a new reason, this scan</param>
/// <param name="Issues">The most recently seen, at most <see cref="MaxIssues"/></param>
public sealed record ScanIssueSummaryDto(int Count, int NewCount, IList<ScanIssueSummaryItemDto> Issues)
{
    public const int MaxIssues = 10;
}

public sealed record ScanIssueSummaryItemDto(string FilePath, MediaErrorReason Reason, int? SeriesId, string? SeriesName);
