namespace Kavita.Models.DTOs.SignalR.Bodies;
#nullable enable

public record DownloadProgressEventBody(string UserName, string DownloadName, float Progress, string? CorrelationId);
