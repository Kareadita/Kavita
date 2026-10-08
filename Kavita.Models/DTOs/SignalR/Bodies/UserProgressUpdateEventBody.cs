namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record UserProgressUpdateEventBody(int UserId, int SeriesId, int VolumeId, int ChapterId, int PagesRead);
