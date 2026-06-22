namespace Kavita.Models.DTOs.Uploads;

public sealed record UploadCoverFileDto
{
    /// <summary>
    /// Id of the Entity
    /// </summary>
    public required int Id { get; set; }
    /// <summary>
    /// Base64 encoding of the file to upload from. Legacy fallback - prefer <see cref="FileName"/>.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Filename of an image already staged in the temp directory (returned by upload/upload-by-url or
    /// upload/upload-by-file). When set, the cover is generated from this file instead of a base64 <see cref="Url"/>.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Lock the cover or not
    /// </summary>
    public bool LockCover { get; set; } = true;
}
