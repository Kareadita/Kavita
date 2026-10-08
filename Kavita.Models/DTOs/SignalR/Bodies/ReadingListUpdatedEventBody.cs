namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ReadingListUpdatedEventBody(int Id);
public sealed record SeriesUpdatedEventBody(int Id);
public sealed record ExternalMetadataUpdateEventBody(int SeriesId);
