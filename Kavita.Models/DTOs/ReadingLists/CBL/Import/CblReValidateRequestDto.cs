namespace Kavita.Models.DTOs.ReadingLists.CBL.Import;

/// <summary>
/// Request body for the re-validate endpoint
/// </summary>
public record CblReValidateRequestDto
{
    public string FileName { get; set; } = string.Empty;
}
