namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record SeriesAddedEventBodyDto(int LibraryId, int SeriesId, string SeriesName);
