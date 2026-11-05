export interface ProgressSave {
  libraryId: number;
  seriesId: number;
  volumeId: number;
  chapterId: number;
  page: number;
  bookScrollId?: string | null;
  /**
   * Send page 0 before this page, in the same queue slot
   */
  resetFirst?: boolean;
}
