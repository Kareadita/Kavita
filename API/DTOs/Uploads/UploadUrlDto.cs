using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Uploads;

public sealed record UploadUrlDto
{
    /// <summary>
    /// External url
    /// </summary>
    [Required]
    public required string Url { get; set; }
}
