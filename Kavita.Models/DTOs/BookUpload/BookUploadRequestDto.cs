using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.BookUpload;

public sealed record BookUploadRequestDto
{
    [Required]
    public int LibraryId { get; init; }
    [Required]
    public required string LibraryFolder { get; init; }
    public string? TargetFolderName { get; init; }
    public BookUploadConflictMode ConflictMode { get; init; } = BookUploadConflictMode.Reject;
}
