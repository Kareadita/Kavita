namespace API.DTOs.Uploads;

public class UploadFileDto
{
    /// <summary>
    /// Id of the Entity
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Base Url encoding of the file to upload from (can be null)
    /// </summary>
    public string Url { get; set; }
}
