namespace API.DTOs;
#nullable enable

/// <summary>
/// A primary and secondary color
/// </summary>
public sealed record ColorScape
{
    public required string? Primary { get; set; }
    public required string? Secondary { get; set; }
}
