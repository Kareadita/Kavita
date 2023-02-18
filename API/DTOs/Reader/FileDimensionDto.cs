namespace API.DTOs.Reader;

public class FileDimensionDto
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int PageNumber { get; set; }
    /// <summary>
    /// The filename of the cached file. If this was nested in a subfolder, the foldername will be appended with _
    /// </summary>
    /// <example>chapter01_page01.png</example>
    public string FileName { get; set; } = default!;
    public bool IsWide { get; set; }
}
