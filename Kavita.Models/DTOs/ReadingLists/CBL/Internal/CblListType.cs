namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// Classification of a CBL reading list, indicating its scope or purpose.
/// </summary>
public enum CblListType
{
    /// <summary>
    /// Unrecognised or unspecified list type
    /// </summary>
    Unknown,
    /// <summary>
    /// A master reading order spanning an entire publisher's output
    /// </summary>
    Master,
    /// <summary>
    /// Crosses multiple fictional universes within a publisher
    /// </summary>
    Interuniversal,
    /// <summary>
    /// Scoped to a single fictional universe
    /// </summary>
    Universal,
    /// <summary>
    /// Focused on a specific super-hero team (e.g. Avengers, Justice League)
    /// </summary>
    Team,
    /// <summary>
    /// Focused on a single character (e.g. Spider-Man, Batman)
    /// </summary>
    Character,
    /// <summary>
    /// Follows a specific story arc or crossover event
    /// </summary>
    Story
}
