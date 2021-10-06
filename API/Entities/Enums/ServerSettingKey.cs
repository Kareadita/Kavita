using System.ComponentModel;

namespace API.Entities.Enums
{
    public enum ServerSettingKey
    {
        [Description("TaskScan")]
        TaskScan = 0,
        [Description("CacheDirectory")]
        CacheDirectory = 1,
        [Description("TaskBackup")]
        TaskBackup = 2,
        [Description("LoggingLevel")]
        LoggingLevel = 3,
        [Description("Port")]
        Port = 4,
        [Description("BackupDirectory")]
        BackupDirectory = 5,
        [Description("AllowStatCollection")]
        AllowStatCollection = 6,
        [Description("EnableOpds")]
        EnableOpds = 7,
        [Description("EnableAuthentication")]
        EnableAuthentication = 8,
        [Description("BaseUrl")]
        BaseUrl = 9

    }
}
