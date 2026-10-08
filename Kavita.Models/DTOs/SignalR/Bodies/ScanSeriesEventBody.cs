namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ScanSeriesEventBody(int LibraryId, int SeriesId, string SeriesName);
public sealed record SeriesAddedEventBody(int LibraryId, int SeriesId, string SeriesName);
public sealed record SeriesRemovedEventBody(int LibraryId, int SeriesId);
