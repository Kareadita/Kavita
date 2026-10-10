namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record UserProgressUpdateEventBodyDto(int UserId, int SeriesId, int VolumeId, int ChapterId, int PagesRead);
