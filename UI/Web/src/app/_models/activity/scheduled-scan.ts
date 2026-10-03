export interface ScheduledScan {
  jobId: string;
  /**
   * Null when every library will be scanned
   */
  libraryId: number | null;
  seriesId: number | null;
  runAtUtc: string;
}
