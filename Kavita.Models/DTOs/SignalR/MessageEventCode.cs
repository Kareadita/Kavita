namespace Kavita.Models.DTOs.SignalR;

/// <summary>
/// Stable keys set on <see cref="SignalRMessageDto.Code"/>. The UI translates these, so never rename a value (see event-message-pipe localization scope)
/// </summary>
public static class MessageEventCode
{
    #region Scan Loop

    public const string ScanListingFolders = "scan-listing-folders";
    public const string ScanReadingFiles = "scan-reading-files";
    public const string ScanGroupingSeries = "scan-grouping-series";
    public const string ScanProcessingSeries = "scan-processing-series";

    #endregion

    #region Scan Problems

    public const string RootFoldersInaccessible = "root-folders-inaccessible";
    public const string RootFoldersEmpty = "root-folders-empty";
    public const string SeriesCollision = "series-collision";
    public const string FilesOutsideFolder = "files-outside-folder";
    public const string ScanSeriesNoRoot = "scan-series-no-root";
    public const string ScanSeriesNotNested = "scan-series-not-nested";
    public const string ScanSeriesNoFiles = "scan-series-no-files";
    public const string ScanSeriesFolderMissing = "scan-series-folder-missing";
    public const string ScanNoWork = "scan-no-work";
    public const string DbWriteFailed = "db-write-failed";

    #endregion

    #region Scan Scheduling

    public const string ScanLibrariesDelayed = "scan-libraries-delayed";
    public const string ScanLibraryDelayed = "scan-library-delayed";
    public const string ScanSeriesDelayed = "scan-series-delayed";

    #endregion

    #region Tasks

    public const string BackupFolderUnwritable = "backup-folder-unwritable";
    public const string BackupExists = "backup-exists";
    public const string CleanupOnHold = "cleanup-on-hold";
    public const string WordCountFailed = "word-count-failed";

    #endregion
}
