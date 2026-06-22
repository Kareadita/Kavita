using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.UserPreferences;

namespace Kavita.Models.DTOs.KavitaPlus.Scrobble;

public sealed record ScrobbleProviderSettingsDto
{
    /// <summary>
    /// Scrobble read progress
    /// </summary>
    public bool ProgressScrobbling { get; set; }
    /// <summary>
    /// Want to Read Sync
    /// </summary>
    public bool WantToReadSync { get; set; }
    /// <summary>
    /// Scrobble ratings
    /// </summary>
    public bool RatingScrobbling { get; set; }
    /// <summary>
    /// Scrobble reviews
    /// </summary>
    public bool ReviewsScrobbling { get; set; }
    /// <summary>
    /// Review Scrobble Target
    /// </summary>
    public ReviewScrobbleTarget ReviewScrobbleTarget { get; set; }
    /// <summary>
    /// Enable for all libraries. Ignoring <see cref="Libraries"/>
    /// </summary>
    /// <remarks>This auto-enables scrobble for newly created libraries</remarks>
    public bool AllLibraries { get; set; }

    /// <summary>
    /// Libraries for which scrobbling is enabled
    /// </summary>
    public List<int> Libraries { get; set; } = [];

    /// <summary>
    /// Highest (inclusive) age rating to scrobble for.
    /// </summary>
    public AgeRating HighestAgeRating { get; set; } = AgeRating.NotApplicable;
    /// <summary>
    /// Triggers if a series hasn't been read for n days and has unread chapters
    /// </summary>
    public ReadStatusTransitionRule InactiveSeriesRule { get; set; } = new();
    /// <summary>
    /// Triggers if a series hasn't been read for n days and has unread chapters
    /// </summary>
    public ReadStatusTransitionRule DroppedSeriesRule { get; set; } = new();
}

public sealed record ReadStatusTransitionRule
{
    /// <summary>
    /// Should Kavita update read status for inactive series?
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// After how many days should the rule trigger?
    /// </summary>
    public int Days { get; set; }
    /// <summary>
    /// To which status should the series be transitioned?
    /// </summary>
    public ScrobbleReadStatus TransitionStatus { get; set; }
    /// <summary>
    /// Exclude series with these publication statuses from the rule
    /// </summary>
    public List<PublicationStatus> ExcludedPublicationStatus { get; set; }
}
