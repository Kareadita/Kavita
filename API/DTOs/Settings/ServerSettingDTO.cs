﻿using API.Services;

namespace API.DTOs.Settings
{
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

        public bool ConvertBookmarkToWebP { get; set; }
        /// <summary>
        /// If the Swagger UI Should be exposed. Does not require authentication, but does require a JWT.
        /// </summary>
        public bool EnableSwaggerUi { get; set; }
    }
}
