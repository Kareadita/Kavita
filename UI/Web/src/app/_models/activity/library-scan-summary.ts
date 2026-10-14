import {ScanIssueSummaryItem} from './scan-issue-summary-item';

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
  /**
   * The most recently seen of problemFiles, at most 10
   */
  recentProblemFiles: ScanIssueSummaryItem[];
  /**
   * No more scan events follow in this job. False for every library but the last in a scan of all libraries
   */
  endsJob: boolean;
}
