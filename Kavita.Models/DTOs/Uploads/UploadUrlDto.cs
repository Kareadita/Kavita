using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Uploads;

public sealed record UploadUrlDto
{
    /// <summary>
    /// External url
    /// </summary>
    [Required]
    public required string Url { get; set; }
    /// <summary>
    /// Does this url resolve to Kavita - This will bypass security checks
    /// </summary>
    public bool IsInternalUrl { get; set; }
}
