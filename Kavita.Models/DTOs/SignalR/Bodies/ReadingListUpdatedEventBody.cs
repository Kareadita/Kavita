namespace Kavita.Models.DTOs.SignalR.Bodies;

public record ReadingListUpdatedEventBody(int Id);
public record SeriesUpdatedEventBody(int Id);
public record ExternalMetadataUpdateEventBody(int SeriesId);
