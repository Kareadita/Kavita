namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ErrorEventBody(string Name, string Title, string SubTitle);
public sealed record InfoEventBody(string Name, string Title, string SubTitle);
