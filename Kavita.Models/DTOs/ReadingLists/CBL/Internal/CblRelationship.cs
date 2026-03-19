namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// A link to a related reading list (e.g. prequel, sequel, companion)
/// Populated from V2 <c>listDetails.relationships[]</c>
/// </summary>
public sealed record CblRelationship
{
    /// <summary>
    /// Display name of the related reading list
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// UUID of the related reading list's CBL file
    /// </summary>
    public string Uuid { get; set; } = string.Empty;
    /// <summary>
    /// Nature of the relationship (e.g. "prequel", "sequel", "companion")
    /// </summary>
    public string Relationship { get; set; } = string.Empty;
}
