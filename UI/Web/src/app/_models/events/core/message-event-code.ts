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
  SeriesCollision = 'series-collision',
  FilesOutsideFolder = 'files-outside-folder',
  ScanSeriesNoRoot = 'scan-series-no-root',
  ScanSeriesNotNested = 'scan-series-not-nested',
  ScanSeriesNoFiles = 'scan-series-no-files',
  ScanSeriesFolderMissing = 'scan-series-folder-missing',
  ScanNoWork = 'scan-no-work',
  DbWriteFailed = 'db-write-failed',

  ScanLibrariesDelayed = 'scan-libraries-delayed',
  ScanLibraryDelayed = 'scan-library-delayed',
  ScanSeriesDelayed = 'scan-series-delayed',

  BackupFolderUnwritable = 'backup-folder-unwritable',
  BackupExists = 'backup-exists',
  CleanupOnHold = 'cleanup-on-hold',
  WordCountFailed = 'word-count-failed',
}
