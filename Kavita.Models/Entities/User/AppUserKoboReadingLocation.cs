namespace Kavita.Models.Entities.User;

/// <summary>
/// Per-user Kobo <c>CurrentBookmark.Location</c> (Value / Type / Source) for a chapter.
/// Separate from <c>AppUserProgress.BookScrollId</c> (web EPUB XPath).
/// </summary>
public class AppUserKoboReadingLocation
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;

    /// <summary>Wire <c>Location.Value</c>. Empty/null means omit Location on emit.</summary>
    public string? LocationValue { get; set; }

    /// <summary>Wire <c>Location.Type</c>.</summary>
    public string? LocationType { get; set; }

    /// <summary>Wire <c>Location.Source</c>.</summary>
    public string? LocationSource { get; set; }
}
