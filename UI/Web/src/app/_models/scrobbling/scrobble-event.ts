export enum ScrobbleEventType {
  ChapterRead = 0,
  AddWantToRead = 1,
  RemoveWantToRead = 2,
  ScoreUpdated = 3,
  Review = 4
}

export interface ScrobbleEvent {
  seriesName: string;
  seriesId: number;
  libraryId: number;
  isProcessed: string;
  scrobbleEventType: ScrobbleEventType;
  rating: number | null;
  processedDateUtc: string;
  lastModifiedUtc: string;
  createdUtc: string;
  volumeNumber: number | null;
  chapterNumber: number | null;
  isErrored: boolean;
  /**
   * Null when not errored
   */
  errorDetails: string | null;

}
