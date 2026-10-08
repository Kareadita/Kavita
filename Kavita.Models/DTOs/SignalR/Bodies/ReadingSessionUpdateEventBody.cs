namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ReadingSessionUpdateEventBody(int UserId, int SessionId);
public sealed record ReadingSessionCloseEventBody(int UserId, int SessionId);
