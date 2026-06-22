using System.ComponentModel;

namespace Kavita.Models.Entities.Enums.Audit;

/// <summary>
/// Why a Kavita+ metadata fetch was triggered. Surfaced on MetadataFetched audit entries so
/// admins can tell an automatic fetch (series added/scanned), an on-demand fetch (visiting the
/// series detail page), a manual match pull, and the scheduled refresh apart.
/// </summary>
public enum MetadataFetchTrigger
{
    /// <summary>
    /// Trigger not recorded (e.g. audit entries written before triggers were tracked). Renders without a label.
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// Fetched automatically when the series was first added during a library scan.
    /// </summary>
    [Description("Series Added")]
    SeriesAdded = 1,
    /// <summary>
    /// Fetched on-demand because a user navigated to the series detail page and the data was stale.
    /// </summary>
    [Description("On Demand")]
    OnDemand = 2,
    /// <summary>
    /// Fetched because a user manually fixed/pulled the match.
    /// </summary>
    [Description("Manual Match")]
    ManualMatch = 3,
    /// <summary>
    /// Fetched by the scheduled daily Kavita+ data refresh job.
    /// </summary>
    [Description("Scheduled Refresh")]
    ScheduledRefresh = 4,
}
