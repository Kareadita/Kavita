namespace Kavita.Models.DTOs.BookUpload;

public sealed record BookUploadFileResultDto
{
    public required string FileName { get; init; }
    public bool Success { get; init; }
    public bool ScanQueued { get; init; }
    public string? DestinationPath { get; init; }
    public string? Error { get; init; }
}
