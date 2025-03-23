namespace API.DTOs;
#nullable enable

/// <summary>
/// A primary and secondary color
/// </summary>
public class ColorScape
{
    public required string? Primary { get; set; }
    public required string? Secondary { get; set; }
}
