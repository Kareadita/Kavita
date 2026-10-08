/**
 * Body of the ScanProgress ended frame
 */
export interface LibraryScanSummary {
  libraryId: number;
  libraryName: string;
  seriesAdded: number;
  seriesRemoved: number;
  chaptersAdded: number;
  /**
   * Existing chapters with a file that changed on disk
   */
  chaptersUpdated: number;
  chaptersRemoved: number;
  /**
   * Every unreadable file in the library, not only ones found this scan
   */
  problemFiles: number;
  newProblemFiles: number;
}
