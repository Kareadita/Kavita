namespace Kavita.Models.DTOs.SignalR.Bodies;

public record ScanSeriesEventBody(int LibraryId, int SeriesId, string SeriesName);
public record SeriesAddedEventBody(int LibraryId, int SeriesId, string SeriesName);
public record SeriesRemovedEventBody(int LibraryId, int SeriesId, string SeriesName);
