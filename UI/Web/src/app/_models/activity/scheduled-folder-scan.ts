export interface ScheduledFolderScan {
  /**
   * Null when the folder is in no library
   */
  libraryId: number | null;
  fromFolderWatcher: boolean;
  /**
   * Soonest job, null once one is queued to run
   */
  runAtUtc: string | null;
  /**
   * Soonest first, at most 5 of folderCount
   */
  folders: string[];
  folderCount: number;
}
