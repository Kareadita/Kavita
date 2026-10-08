namespace Kavita.Models.DTOs.SignalR.Bodies;

/// <param name="ChaptersUpdated">Existing chapters with a file that changed on disk</param>
/// <param name="ProblemFiles">Every unreadable file in the library, not only ones found this scan</param>
public sealed record LibraryScanEndedEventBody(int LibraryId, string LibraryName,
    int SeriesAdded, int SeriesRemoved,
    int ChaptersAdded, int ChaptersUpdated, int ChaptersRemoved,
    int ProblemFiles, int NewProblemFiles);
