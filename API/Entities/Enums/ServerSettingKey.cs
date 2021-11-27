using System.ComponentModel;

namespace API.Entities.Enums
{
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
        InstallId = 10

    }
}
