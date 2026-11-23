namespace Kavita.Models.Kobo;

/// <summary>
/// Records information about a Given Kobo
/// </summary>
public sealed record KoboDeviceProfile
{
    public required string Name { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsGreyscale { get; set; }
    // I can put more information here to help with tie-back
}
