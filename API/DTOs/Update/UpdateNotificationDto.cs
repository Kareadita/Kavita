namespace API.DTOs.Update;

/// <summary>
/// Update Notification denoting a new release available for user to update to
/// </summary>
public class UpdateNotificationDto
{
    /// <summary>
    /// Current installed Version
    /// </summary>
    public required string CurrentVersion { get; init; }
    /// <summary>
    /// Semver of the release version
    /// <example>0.4.3</example>
    /// </summary>
    public required string UpdateVersion { get; set; }
    /// <summary>
    /// Release body in HTML
    /// </summary>
    public required string UpdateBody { get; init; }
    /// <summary>
    /// Title of the release
    /// </summary>
    public required string UpdateTitle { get; init; }
    /// <summary>
    /// Github Url
    /// </summary>
    public required string UpdateUrl { get; init; }
    /// <summary>
    /// If this install is within Docker
    /// </summary>
    public bool IsDocker { get; init; }
    /// <summary>
    /// Is this a pre-release
    /// </summary>
    public bool IsPrerelease { get; init; }
    /// <summary>
    /// Date of the publish
    /// </summary>
    public required string PublishDate { get; init; }
    /// <summary>
    /// Is the server on a nightly within this release
    /// </summary>
    public bool IsOnNightlyInRelease { get; set; }
    /// <summary>
    /// Is the server on an older version
    /// </summary>
    public bool IsReleaseNewer { get; set; }
    /// <summary>
    /// Is the server on this version
    /// </summary>
    public bool IsReleaseEqual { get; set; }
}
