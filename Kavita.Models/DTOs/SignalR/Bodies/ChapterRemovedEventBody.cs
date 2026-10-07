namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ChapterRemovedEventBody(int SeriesId, int ChapterId);
public sealed record ChapterUpdatedEventBody(int SeriesId, int ChapterId);
