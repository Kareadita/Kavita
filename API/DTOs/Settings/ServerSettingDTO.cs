using System;
using API.Services;

namespace API.DTOs.Settings;

public class ServerSettingDto
{
    public string CacheDirectory { get; set; } = default!;
    public string TaskScan { get; set; } = default!;
    /// <summary>
    /// Logging level for server. Managed in appsettings.json.
    /// </summary>
    public string LoggingLevel { get; set; } = default!;
    public string TaskBackup { get; set; } = default!;
    /// <summary>
    /// Port the server listens on. Managed in appsettings.json.
    /// </summary>
    public int Port { get; set; }
    /// <summary>
    /// Allows anonymous information to be collected and sent to KavitaStats
    /// </summary>
    public bool AllowStatCollection { get; set; }
    /// <summary>
    /// Enables OPDS connections to be made to the server.
    /// </summary>
    public bool EnableOpds { get; set; }
    /// <summary>
    /// Base Url for the kavita. Requires restart to take effect.
    /// </summary>
    public string BaseUrl { get; set; } = default!;
    /// <summary>
    /// Where Bookmarks are stored.
    /// </summary>
    /// <remarks>If null or empty string, will default back to default install setting aka <see cref="DirectoryService.BookmarkDirectory"/></remarks>
    public string BookmarksDirectory { get; set; } = default!;
    /// <summary>
    /// Email service to use for the invite user flow, forgot password, etc.
    /// </summary>
    /// <remarks>If null or empty string, will default back to default install setting aka <see cref="EmailService.DefaultApiUrl"/></remarks>
    public string EmailServiceUrl { get; set; } = default!;
    public string InstallVersion { get; set; } = default!;
    /// <summary>
    /// Represents a unique Id to this Kavita installation. Only used in Stats to identify unique installs.
    /// </summary>
    public string InstallId { get; set; } = default!;
    /// <summary>
    /// If the server should save bookmarks as WebP encoding
    /// </summary>
    public bool ConvertBookmarkToWebP { get; set; }
    /// <summary>
    /// If the Swagger UI Should be exposed. Does not require authentication, but does require a JWT.
    /// </summary>
    [Obsolete("Being removed in v0.7 in favor of dedicated hosted api")]
    public bool EnableSwaggerUi { get; set; }
    /// <summary>
    /// The amount of Backups before cleanup
    /// </summary>
    /// <remarks>Value should be between 1 and 30</remarks>
    public int TotalBackups { get; set; } = 30;
    /// <summary>
    /// If Kavita should watch the library folders and process changes
    /// </summary>
    public bool EnableFolderWatching { get; set; } = true;
    /// <summary>
    /// Total number of days worth of logs to keep at a given time.
    /// </summary>
    /// <remarks>Value should be between 1 and 30</remarks>
    public int TotalLogs { get; set; }
    /// <summary>
    /// If the server should save covers as WebP encoding
    /// </summary>
    public bool ConvertCoverToWebP { get; set; }
}
