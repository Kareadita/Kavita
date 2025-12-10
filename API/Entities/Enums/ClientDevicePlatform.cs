using System.ComponentModel;

namespace API.Entities.Enums;

public enum ClientDevicePlatform
{
    [Description("Unknown")]
    Unknown = 0,
    [Description("Windows")]
    Windows = 1,
    [Description("macOS")]
    MacOs = 2,
    [Description("IOs")]
    Ios = 3,
    [Description("Linux")]
    Linux = 4,
    [Description("Android")]
    Android = 5,
}
