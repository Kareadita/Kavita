namespace Kavita.Models.Entities.Enums;

/// <summary>
/// Identifies which read-status transition rule produced a scrobble event/history row.
/// </summary>
public enum TransitionRuleKind
{
    /// <summary>
    /// Series hasn't been read for N days and has unread chapters (On Hold style rule)
    /// </summary>
    Inactive = 0,
    /// <summary>
    /// Series hasn't been read for N days (Dropped style rule)
    /// </summary>
    Dropped = 1
}
