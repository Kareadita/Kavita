namespace Kavita.Models.DTOs.Font;

/// <summary>
/// Result of attempting to delete a font family
/// </summary>
public sealed record FontDeleteResultDto
{
    /// <summary>
    /// True when the family (all of its user-provided files) was removed
    /// </summary>
    public bool Deleted { get; init; }
    /// <summary>
    /// True when the family is currently selected by one or more users. When blocked, an admin may re-issue
    /// the request with force to delete it anyway.
    /// </summary>
    public bool InUse { get; init; }
}
