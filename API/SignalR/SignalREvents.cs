namespace API.SignalR
{
    public static class SignalREvents
    {
        public const string UpdateVersion = "UpdateVersion";
        public const string ScanSeries = "ScanSeries";
        /// <summary>
        /// Event during Refresh Metadata for cover image change
        /// </summary>
        public const string RefreshMetadata = "RefreshMetadata";
        /// <summary>
        /// Event sent out during Refresh Metadata for progress tracking
        /// </summary>
        public const string RefreshMetadataProgress = "RefreshMetadataProgress";
        public const string ScanLibrary = "ScanLibrary";
        public const string SeriesAdded = "SeriesAdded";
        public const string SeriesRemoved = "SeriesRemoved";
        public const string ScanLibraryProgress = "ScanLibraryProgress";
        public const string OnlineUsers = "OnlineUsers";
        public const string SeriesAddedToCollection = "SeriesAddedToCollection";
        public const string ScanLibraryError = "ScanLibraryError";
        /// <summary>
        /// Event sent out during backing up the database
        /// </summary>
        public const string BackupDatabaseProgress = "BackupDatabaseProgress";
        /// <summary>
        /// Event sent out during cleaning up temp and cache folders
        /// </summary>
        public const string CleanupProgress = "CleanupProgress";
    }
}
