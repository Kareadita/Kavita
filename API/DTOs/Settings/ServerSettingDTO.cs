namespace API.DTOs
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
        /// Enables Authentication on the server. Defaults to true.
        /// </summary>
        public bool EnableAuthentication { get; set; }
    }
}
