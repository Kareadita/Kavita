namespace Kavita.Models.DTOs.SignalR.Bodies;

public record LibraryScanProgressEventBody(int LibraryId, float? Progress, int? LeftToProgress, int? TotalProgress)
{
    public string LibraryName { get; set; }
    /// <summary>
    /// Can be empty when not applicable
    /// </summary>
    public string SeriesName { get; set; }
}
