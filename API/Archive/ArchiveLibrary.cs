namespace API.Archive;

/// <summary>
/// Represents which library should handle opening this library
/// </summary>
public enum ArchiveLibrary
{
    /// <summary>
    /// The underlying archive cannot be opened
    /// </summary>
    NotSupported = 0,
    /// <summary>
    /// The underlying archive can be opened by SharpCompress
    /// </summary>
    SharpCompress = 1,
    /// <summary>
    /// The underlying archive can be opened by default .NET
    /// </summary>
    Default = 2
}
