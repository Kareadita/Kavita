namespace API.DTOs.Theme;
#nullable enable

/// <summary>
/// A set of colors for the color scape system in the UI
/// </summary>
public class ColorScapeDto
{
    public string? Primary { get; set; }
    public string? Secondary { get; set; }

    public ColorScapeDto(string? primary, string? secondary)
    {
        Primary = primary;
        Secondary = secondary;
    }

    public static readonly ColorScapeDto Empty = new ColorScapeDto(null, null);
}
