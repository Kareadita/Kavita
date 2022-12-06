namespace API.DTOs.System;

public class DirectoryDto
{
    /// <summary>
    /// Name of the directory
    /// </summary>
    public string Name { get; set; } = default!;
    /// <summary>
    /// Full Directory Path
    /// </summary>
    public string FullPath { get; set; } = default!;
}
