namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// Categorisation of an issue's role within a reading list (V2 only).
/// </summary>
public enum CblIssueType
{
    /// <summary>
    /// Unrecognised or unspecified issue type.
    /// </summary>
    Unknown,
    /// <summary>
    /// A core issue in an event storyline.
    /// </summary>
    EventCore,
    /// <summary>
    /// A tie-in issue that crosses over with an event.
    /// </summary>
    EventTieIn,
    /// <summary>
    /// A standalone one-shot related to an event.
    /// </summary>
    EventOneShot,
    /// <summary>
    /// A regular ongoing series issue.
    /// </summary>
    Ongoing
}
