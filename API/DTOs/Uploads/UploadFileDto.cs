namespace API.DTOs.Uploads;

public class UploadFileDto
{
    /// <summary>
    /// Id of the Entity
    /// </summary>
    public required int Id { get; set; }
    /// <summary>
    /// Base Url encoding of the file to upload from (can be null)
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Lock the cover or not
    /// </summary>
    public bool LockCover { get; set; } = true;
}
