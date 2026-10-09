namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ScanSeriesEventBodyDto(int LibraryId, int SeriesId, string SeriesName);
