namespace API.SignalR
{
    public static class SignalREvents
    {
        /// <summary>
        /// An update is available for the Kavita instance
        /// </summary>
        public const string UpdateAvailable = "UpdateAvailable";
        /// <summary>
        /// Used to tell when a scan series completes
        /// </summary>
        public const string ScanSeries = "ScanSeries";
        /// <summary>
        /// Event sent out during Refresh Metadata for progress tracking
        /// </summary>
        public const string RefreshMetadataProgress = "RefreshMetadataProgress";
        /// <summary>
        /// Series is added to server
        /// </summary>
        public const string SeriesAdded = "SeriesAdded";
        /// <summary>
        /// Series is removed from server
        /// </summary>
        public const string SeriesRemoved = "SeriesRemoved";
        /// <summary>
        /// Progress event for Scan library
        /// </summary>
        public const string ScanLibraryProgress = "ScanLibraryProgress";
        /// <summary>
        /// When a user is connects/disconnects from server
        /// </summary>
        public const string OnlineUsers = "OnlineUsers";
        /// <summary>
        /// When a series is added to a collection
        /// </summary>
        public const string SeriesAddedToCollection = "SeriesAddedToCollection";
        /// <summary>
        /// When an error occurs during a scan library task
        /// </summary>
        public const string ScanLibraryError = "ScanLibraryError";
        /// <summary>
        /// Event sent out during backing up the database
        /// </summary>
        public const string BackupDatabaseProgress = "BackupDatabaseProgress";
        /// <summary>
        /// Event sent out during cleaning up temp and cache folders
        /// </summary>
        public const string CleanupProgress = "CleanupProgress";
        /// <summary>
        /// Event sent out during downloading of files
        /// </summary>
        public const string DownloadProgress = "DownloadProgress";
        /// <summary>
        /// A cover was updated
        /// </summary>
        public const string CoverUpdate = "CoverUpdate";
        /// <summary>
        /// A custom site theme was removed or added
        /// </summary>
        public const string SiteThemeProgress = "SiteThemeProgress";

    }
}
