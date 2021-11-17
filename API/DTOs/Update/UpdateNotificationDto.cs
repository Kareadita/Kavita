using System;

namespace API.DTOs.Update
{
    /// <summary>
    /// Update Notification denoting a new release available for user to update to
    /// </summary>
    public class UpdateNotificationDto
    {
        /// <summary>
        /// Current installed Version
        /// </summary>
        public string CurrentVersion { get; init; }
        /// <summary>
        /// Semver of the release version
        /// <example>0.4.3</example>
        /// </summary>
        public string UpdateVersion { get; init; }
        /// <summary>
        /// Release body in HTML
        /// </summary>
        public string UpdateBody { get; init; }
        /// <summary>
        /// Title of the release
        /// </summary>
        public string UpdateTitle { get; init; }
        /// <summary>
        /// Github Url
        /// </summary>
        public string UpdateUrl { get; init; }
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
        public string PublishDate { get; init; }
    }
}
