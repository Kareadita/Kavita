namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record DebugExportFailedEventBodyDto(string Name, string Title, string SubTitle, string ExportId, string Error);
