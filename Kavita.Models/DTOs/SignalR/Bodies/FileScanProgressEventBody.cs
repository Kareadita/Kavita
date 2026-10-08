namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record FileScanProgressEventBody(int LibraryId, string LibraryName, int? Current, int? Total, float? Progress);
