using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public record FileScanProgressEventBody(int LibraryId, int? Current, int? Total)
{
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string Filename { get; set; }
    public string LibraryName { get; set; }
    public DateTime EventTime { get; set; }
    public float? Progress { get; set; }
}
