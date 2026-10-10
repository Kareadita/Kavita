namespace Kavita.Models.DTOs.SignalR.Bodies;
#nullable enable

public sealed record DownloadProgressEventBodyDto(string UserName, string DownloadName, float Progress, string? CorrelationId);
