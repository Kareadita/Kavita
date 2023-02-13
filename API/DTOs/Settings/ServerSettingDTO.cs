using System;
using System.ComponentModel.DataAnnotations;
using API.Services;

namespace API.DTOs.Settings;

public class ServerSettingDto
{
    public string CacheDirectory { get; set; }
    public string TaskScan { get; set; }
    /// <summary>
    /// Logging level for server. Managed in appsettings.json.
    /// </summary>
    public string LoggingLevel { get; set; }
    public string TaskBackup { get; set; }
    /// <summary>
    /// Port the server listens on. Managed in appsettings.json.
    /// </summary>
    public int Port { get; set; }
    /// <summary>
    /// Comma separated list of ip addresses the server listens on. Managed in appsettings.json
    /// </summary>
    public string IpAddresses { get; set; }
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
    public string BaseUrl { get; set; }
    /// <summary>
    /// Where Bookmarks are stored.
    /// </summary>
    /// <remarks>If null or empty string, will default back to default install setting aka <see cref="DirectoryService.BookmarkDirectory"/></remarks>
    public string BookmarksDirectory { get; set; }
    /// <summary>
    /// Email service to use for the invite user flow, forgot password, etc.
    /// </summary>
    /// <remarks>If null or empty string, will default back to default install setting aka <see cref="EmailService.DefaultApiUrl"/></remarks>
    public string EmailServiceUrl { get; set; }
    public string InstallVersion { get; set; }
    /// <summary>
    /// Represents a unique Id to this Kavita installation. Only used in Stats to identify unique installs.
    /// </summary>
    public string InstallId { get; set; }
    /// <summary>
    /// If the server should save bookmarks as WebP encoding
    /// </summary>
    public bool ConvertBookmarkToWebP { get; set; }
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
    /// <summary>
    /// The Host name (ie Reverse proxy domain name) for the server
    /// </summary>
    public string HostName { get; set; }
}
