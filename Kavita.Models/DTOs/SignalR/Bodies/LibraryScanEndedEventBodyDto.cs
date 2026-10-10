using System.Collections.Generic;
using Kavita.Models.DTOs.MediaErrors;

namespace Kavita.Models.DTOs.SignalR.Bodies;

/// <param name="ChaptersUpdated">Existing chapters with a file that changed on disk</param>
/// <param name="ProblemFiles">Every unreadable file in the library, not only ones found this scan</param>
/// <param name="RecentProblemFiles">The most recently seen of <see cref="ProblemFiles"/>, at most <see cref="ScanIssueSummaryDto.MaxIssues"/></param>
public sealed record LibraryScanEndedEventBodyDto(int LibraryId, string LibraryName,
    int SeriesAdded, int SeriesRemoved,
    int ChaptersAdded, int ChaptersUpdated, int ChaptersRemoved,
    int ProblemFiles, int NewProblemFiles, IList<ScanIssueSummaryItemDto> RecentProblemFiles);
