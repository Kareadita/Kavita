namespace Kavita.Models.Entities.Enums;

public enum DownloadFormat
{
    /// <summary>
    /// Download as whatever is on disk
    /// </summary>
    Default = 0,
    /// <summary>
    /// Convert to kepub is possible
    /// </summary>
    Kepub = 1,
    /// <summary>
    /// Convert to epub when needed, and possible
    /// </summary>
    Epub = 2,
}
