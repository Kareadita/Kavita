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
    [Obsolete("Not supported as of v0.5.1")]
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
    [Obsolete("Use Email settings instead")]
    EmailServiceUrl = 13,
    /// <summary>
    /// If Kavita should save bookmarks as WebP images
    /// </summary>
    [Obsolete("Use EncodeMediaAs instead")]
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
    [Obsolete("Use EncodeMediaAs instead")]
    [Description("ConvertCoverToWebP")]
    ConvertCoverToWebP = 19,
    /// <summary>
    /// The Host name (ie Reverse proxy domain name) for the server. Used for email link generation
    /// </summary>
    [Description("HostName")]
    HostName = 20,
    /// <summary>
    /// Ip addresses the server listens on. Not managed in DB. Managed in appsettings.json and synced to DB.
    /// </summary>
    [Description("IpAddresses")]
    IpAddresses = 21,
    /// <summary>
    /// Encode all media as PNG/WebP/AVIF/etc.
    /// </summary>
    /// <remarks>As of v0.7.3 this replaced ConvertCoverToWebP and ConvertBookmarkToWebP</remarks>
    [Description("EncodeMediaAs")]
    EncodeMediaAs = 22,
    /// <summary>
    /// A Kavita+ Subscription license key
    /// </summary>
    [Description("LicenseKey")]
    LicenseKey = 23,
    /// <summary>
    /// The size in MB for Caching API data
    /// </summary>
    [Description("Cache")]
    CacheSize = 24,
    /// <summary>
    /// How many Days since today in the past for reading progress, should content be considered for On Deck, before it gets removed automatically
    /// </summary>
    [Description("OnDeckProgressDays")]
    OnDeckProgressDays = 25,
    /// <summary>
    /// How many Days since today in the past for chapter updates, should content be considered for On Deck, before it gets removed automatically
    /// </summary>
    [Description("OnDeckUpdateDays")]
    OnDeckUpdateDays = 26,
    /// <summary>
    /// The size of the cover image thumbnail. Defaults to <see cref="CoverImageSize"/>.Default
    /// </summary>
    [Description("CoverImageSize")]
    CoverImageSize = 27,
    #region EmailSettings
    /// <summary>
    /// The address of the emailer host
    /// </summary>
    [Description("EmailSenderAddress")]
    EmailSenderAddress = 28,
    /// <summary>
    /// What the email name should be
    /// </summary>
    [Description("EmailSenderDisplayName")]
    EmailSenderDisplayName = 29,
    [Description("EmailAuthUserName")]
    EmailAuthUserName = 30,
    [Description("EmailAuthPassword")]
    EmailAuthPassword = 31,
    [Description("EmailHost")]
    EmailHost = 32,
    [Description("EmailPort")]
    EmailPort = 33,
    [Description("EmailEnableSsl")]
    EmailEnableSsl = 34,
    /// <summary>
    /// Number of bytes that the sender allows to be sent through
    /// </summary>
    [Description("EmailSizeLimit")]
    EmailSizeLimit = 35,
    /// <summary>
    /// Should Kavita use config/templates for Email templates or the default ones
    /// </summary>
    [Description("EmailCustomizedTemplates")]
    EmailCustomizedTemplates = 36,
    #endregion
    /// <summary>
    /// When the cleanup task should run - Critical to keeping Kavita working
    /// </summary>
    [Description("TaskCleanup")]
    TaskCleanup = 37,
    /// <summary>
    /// The Date Kavita was first installed
    /// </summary>
    [Description("FirstInstallDate")]
    FirstInstallDate = 38,
    /// <summary>
    /// The Version of Kavita on the first run
    /// </summary>
    [Description("FirstInstallVersion")]
    FirstInstallVersion = 39,
}
