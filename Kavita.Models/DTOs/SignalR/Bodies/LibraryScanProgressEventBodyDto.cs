namespace Kavita.Models.DTOs.SignalR.Bodies;
#nullable enable

public sealed record LibraryScanProgressEventBodyDto(int LibraryId, float? Progress, int? LeftToProcess, int? TotalToProcess)
{
    public string LibraryName { get; set; }
    /// <summary>
    /// Can be empty when not applicable
    /// </summary>
    public string SeriesName { get; set; }
    /// <summary>
    /// Set on every frame of a ScanSeries job, null on a library scan
    /// </summary>
    public SeriesScanTargetDto? SeriesScan { get; set; }
}
