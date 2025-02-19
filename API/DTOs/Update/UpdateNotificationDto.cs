using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;

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
    public required string UpdateTitle { get; set; }
    /// <summary>
    /// Github Url
    /// </summary>
    public required string UpdateUrl { get; set; }
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
    public required string PublishDate { get; set
        ; }
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

    public IList<string> Added { get; set; }
    public IList<string> Removed { get; set; }
    public IList<string> Changed { get; set; }
    public IList<string> Fixed { get; set; }
    public IList<string> Theme { get; set; }
    public IList<string> Developer { get; set; }
    public IList<string> Api { get; set; }
    public IList<string> FeatureRequests { get; set; }
    /// <summary>
    /// The part above the changelog part
    /// </summary>
    public string BlogPart { get; set; }
}
