namespace Kavita.Models.DTOs.Settings;

public enum KepubifyOffReason
{
    /// <summary>
    /// EnableKepubify is unticked
    /// </summary>
    Disabled = 0,
    FileMissing = 1,
    /// <summary>
    /// The shipped binary does not match any known release. EnableKepubify is saved off
    /// </summary>
    HashMismatch = 2,
    /// <summary>
    /// The binary could not be started, timed out, or did not print a kepubify version
    /// </summary>
    VersionCheckFailed = 3,
}
