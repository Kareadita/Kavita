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
  lastModified: string;
  created: string;
  volumeNumber: number | null;
  chapterNumber: number | null;
}
