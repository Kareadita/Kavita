using System.ComponentModel;

namespace API.Entities.Enums.Device;

public enum DevicePlatform
{
    [Description("Custom")]
    Custom = 0,
    /// <summary>
    /// PocketBook device, email ends in @pbsync.com
    /// </summary>
    [Description("PocketBook")]
    PocketBook = 1,
    /// <summary>
    /// Kindle device, email ends in @kindle.com
    /// </summary>
    [Description("Kindle")]
    Kindle = 2,
    /// <summary>
    /// Kobo device,
    /// </summary>
    [Description("Kobo")]
    Kobo = 3,

}
