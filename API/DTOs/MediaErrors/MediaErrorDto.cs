using System;

namespace API.DTOs.MediaErrors;

public class MediaErrorDto
{
    /// <summary>
    /// Format Type (RAR, ZIP, 7Zip, Epub, PDF)
    /// </summary>
    public required string Extension { get; set; }
    /// <summary>
    /// Full Filepath to the file that has some issue
    /// </summary>
    public required string FilePath { get; set; }
    /// <summary>
    /// Developer defined string
    /// </summary>
    public string Comment { get; set; }
    /// <summary>
    /// Exception message
    /// </summary>
    public string Details { get; set; }

    public DateTime CreatedUtc { get; set; }
}
