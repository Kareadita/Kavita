namespace Kavita.Models.DTOs.SignalR.Bodies;

/// <param name="ExportId">Passed back to <c>GET api/server/debug-export/{exportId}</c> to download the zip</param>
public sealed record DebugExportReadyEventBodyDto(string Name, string Title, string SubTitle, string ExportId, long Bytes);
