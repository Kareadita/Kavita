namespace API.DTOs
{
    public class ServerSettingDto
    {
        public string CacheDirectory { get; set; }
        public string TaskScan { get; set; }
        public string LoggingLevel { get; set; }
        public string TaskBackup { get; set; }
        public int Port { get; set; }
        public bool AllowStatCollection { get; set; }
    }
}