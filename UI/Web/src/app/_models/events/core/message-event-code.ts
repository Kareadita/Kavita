/**
 * Mirrors MessageEventCode.cs. The server can send a code this build does not know, so a value may fall outside this enum
 */
export enum MessageEventCode {
  ScanListingFolders = 'scan-listing-folders',
  ScanReadingFiles = 'scan-reading-files',
  ScanGroupingSeries = 'scan-grouping-series',
  ScanProcessingSeries = 'scan-processing-series',

  RootFoldersInaccessible = 'root-folders-inaccessible',
  RootFoldersEmpty = 'root-folders-empty',
  RootFoldersUnreadable = 'root-folders-unreadable',
  UnreadableFolders = 'unreadable-folders',
  SeriesCollision = 'series-collision',
  FilesOutsideFolder = 'files-outside-folder',
  ScanSeriesNoRoot = 'scan-series-no-root',
  ScanSeriesNotNested = 'scan-series-not-nested',
  ScanSeriesNoFiles = 'scan-series-no-files',
  ScanSeriesFolderMissing = 'scan-series-folder-missing',
  ScanNoWork = 'scan-no-work',
  DbWriteFailed = 'db-write-failed',
  LibraryScanFailed = 'library-scan-failed',
  SeriesUpdateFailed = 'series-update-failed',
  SeriesRemovalFailed = 'series-removal-failed',

  ScanLibrariesDelayed = 'scan-libraries-delayed',
  ScanLibraryDelayed = 'scan-library-delayed',
  ScanSeriesDelayed = 'scan-series-delayed',

  FolderWatchFallback = 'folder-watch-fallback',

  BackupFolderUnwritable = 'backup-folder-unwritable',
  BackupExists = 'backup-exists',
  CleanupOnHold = 'cleanup-on-hold',
  WordCountFailed = 'word-count-failed',
}
