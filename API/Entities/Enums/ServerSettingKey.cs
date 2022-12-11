using System;
using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// 15 is blocked as it was EnableSwaggerUi, which is no longer used
/// </summary>
public enum ServerSettingKey
{
    /// <summary>
    /// Cron format for how often full library scans are performed.
    /// </summary>
    [Description("TaskScan")]
    TaskScan = 0,
    /// <summary>
    /// Where files are cached. Not currently used.
    /// </summary>
    [Description("CacheDirectory")]
    CacheDirectory = 1,
    /// <summary>
    /// Cron format for how often backups are taken.
    /// </summary>
    [Description("TaskBackup")]
    TaskBackup = 2,
    /// <summary>
    /// Logging level for Server. Not managed in DB. Managed in appsettings.json and synced to DB.
    /// </summary>
    [Description("LoggingLevel")]
    LoggingLevel = 3,
    /// <summary>
    /// Port server listens on. Not managed in DB. Managed in appsettings.json and synced to DB.
    /// </summary>
    [Description("Port")]
    Port = 4,
    /// <summary>
    /// Where the backups are stored.
    /// </summary>
    [Description("BackupDirectory")]
    BackupDirectory = 5,
    /// <summary>
    /// Allow anonymous data to be reported to KavitaStats
    /// </summary>
    [Description("AllowStatCollection")]
    AllowStatCollection = 6,
    /// <summary>
    /// Is OPDS enabled for the server
    /// </summary>
    [Description("EnableOpds")]
    EnableOpds = 7,
    /// <summary>
    /// Is Authentication needed for non-admin accounts
    /// </summary>
    /// <remarks>Deprecated. This is no longer used v0.5.1+. Assume Authentication is always in effect</remarks>
    [Description("EnableAuthentication")]
    EnableAuthentication = 8,
    /// <summary>
    /// Base Url for the server. Not Implemented.
    /// </summary>
    [Description("BaseUrl")]
    BaseUrl = 9,
    /// <summary>
    /// Represents this installation of Kavita. Is tied to Stat reporting but has no information about user or files.
    /// </summary>
    [Description("InstallId")]
    InstallId = 10,
    /// <summary>
    /// Represents the version the software is running.
    /// </summary>
    /// <remarks>This will be updated on Startup to the latest release. Provides ability to detect if certain migrations need to be run.</remarks>
    [Description("InstallVersion")]
    InstallVersion = 11,
    /// <summary>
    /// Location of where bookmarks are stored
    /// </summary>
    [Description("BookmarkDirectory")]
    BookmarkDirectory = 12,
    /// <summary>
    /// If SMTP is enabled on the server
    /// </summary>
    [Description("CustomEmailService")]
    EmailServiceUrl = 13,
    /// <summary>
    /// If Kavita should save bookmarks as WebP images
    /// </summary>
    [Description("ConvertBookmarkToWebP")]
    ConvertBookmarkToWebP = 14,
    /// <summary>
    /// Total Number of Backups to maintain before cleaning. Default 30, min 1.
    /// </summary>
    [Description("TotalBackups")]
    TotalBackups = 16,
    /// <summary>
    /// If Kavita should watch the library folders and process changes
    /// </summary>
    [Description("EnableFolderWatching")]
    EnableFolderWatching = 17,
    /// <summary>
    /// Total number of days worth of logs to keep
    /// </summary>
    [Description("TotalLogs")]
    TotalLogs = 18,
    /// <summary>
    /// If Kavita should save covers as WebP images
    /// </summary>
    [Description("ConvertCoverToWebP")]
    ConvertCoverToWebP = 19,
}
