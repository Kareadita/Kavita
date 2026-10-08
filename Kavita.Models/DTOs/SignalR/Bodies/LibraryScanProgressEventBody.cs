namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record LibraryScanProgressEventBody(int LibraryId, float? Progress, int? LeftToProcess, int? TotalToProcess)
{
    public string LibraryName { get; set; }
    /// <summary>
    /// Can be empty when not applicable
    /// </summary>
    public string SeriesName { get; set; }
}
