namespace API.Archive
{
    /// <summary>
    /// Represents which library should handle opening this library
    /// </summary>
    public enum ArchiveLibrary
    {
        NotSupported = 0,
        SharpCompress = 1,
        Default = 2
    }
}