using System;
using API.Entities.Enums;
using API.Services;

namespace API.DTOs.Settings;

public class ServerSettingDto
{

    public string CacheDirectory { get; set; } = default!;
    public string TaskScan { get; set; } = default!;
    public string TaskBackup { get; set; } = default!;
    public string TaskCleanup { get; set; } = default!;
    /// <summary>
    /// Logging level for server. Managed in appsettings.json.
    /// </summary>
    public string LoggingLevel { get; set; } = default!;
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
    public string BaseUrl { get; set; } = default!;
    /// <summary>
    /// Where Bookmarks are stored.
    /// </summary>
    /// <remarks>If null or empty string, will default back to default install setting aka <see cref="DirectoryService.BookmarkDirectory"/></remarks>
    public string BookmarksDirectory { get; set; } = default!;
    public string InstallVersion { get; set; } = default!;
    /// <summary>
    /// Represents a unique Id to this Kavita installation. Only used in Stats to identify unique installs.
    /// </summary>
    public string InstallId { get; set; } = default!;
    /// <summary>
    /// The format that should be used when saving media for Kavita
    /// </summary>
    /// <example>This includes things like: Covers, Bookmarks, Favicons</example>
    public EncodeFormat EncodeMediaAs { get; set; }

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
    /// The Host name (ie Reverse proxy domain name) for the server
    /// </summary>
    public string HostName { get; set; }
    /// <summary>
    /// The size in MB for Caching API data
    /// </summary>
    public long CacheSize { get; set; }
    /// <summary>
    /// How many Days since today in the past for reading progress, should content be considered for On Deck, before it gets removed automatically
    /// </summary>
    public int OnDeckProgressDays { get; set; }
    /// <summary>
    /// How many Days since today in the past for chapter updates, should content be considered for On Deck, before it gets removed automatically
    /// </summary>
    public int OnDeckUpdateDays { get; set; }
    /// <summary>
    /// How large the cover images should be
    /// </summary>
    public CoverImageSize CoverImageSize { get; set; }
    /// <summary>
    /// SMTP Configuration
    /// </summary>
    public SmtpConfigDto SmtpConfig { get; set; }
    /// <summary>
    /// The Date Kavita was first installed
    /// </summary>
    public DateTime? FirstInstallDate { get; set; }
    /// <summary>
    /// The Version of Kavita on the first run
    /// </summary>
    public string? FirstInstallVersion { get; set; }

    /// <summary>
    /// Are at least some basics filled in
    /// </summary>
    /// <returns></returns>
    public bool IsEmailSetup()
    {
        return !string.IsNullOrEmpty(SmtpConfig.Host)
               && !string.IsNullOrEmpty(SmtpConfig.SenderAddress)
               && !string.IsNullOrEmpty(HostName);
    }

    /// <summary>
    /// Are at least some basics filled in, but not hostname as not required for Send to Device
    /// </summary>
    /// <returns></returns>
    public bool IsEmailSetupForSendToDevice()
    {
        return !string.IsNullOrEmpty(SmtpConfig.Host)
               && !string.IsNullOrEmpty(SmtpConfig.SenderAddress);
    }
}
