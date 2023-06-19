export enum ScrobbleEventType {
  ChapterRead = 0,
  AddWantToRead = 1,
  RemoveWantToRead = 2,
  ScoreUpdated = 3
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
  volumeNumber: number | null;
  chapterNumber: number | null;
}
