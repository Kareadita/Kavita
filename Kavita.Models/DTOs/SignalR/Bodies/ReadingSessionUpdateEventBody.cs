namespace Kavita.Models.DTOs.SignalR.Bodies;

public record ReadingSessionUpdateEventBody(int UserId, int SessionId);
public record ReadingSessionCloseEventBody(int UserId, int SessionId);
