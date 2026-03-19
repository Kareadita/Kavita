namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// An external source from which a reading list was derived
/// Populated from V2 <c>listDetails.source[]</c>
/// </summary>
public sealed record CblSource
{
    /// <summary>
    /// Name of the source (e.g. "Comic Book Herald")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// URL pointing to the source material
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
