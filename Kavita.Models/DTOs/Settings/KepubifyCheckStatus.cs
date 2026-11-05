namespace Kavita.Models.DTOs.Settings;

public enum KepubifyCheckStatus
{
    /// <summary>
    /// The startup check has not finished yet. Kepub is treated as off until it does
    /// </summary>
    Checking = 0,
    Supported = 1,
    NotSupported = 2,
}
