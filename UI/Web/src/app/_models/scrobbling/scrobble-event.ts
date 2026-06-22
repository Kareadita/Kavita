import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {ScrobbleReadStatus} from "../kavitaplus/scrobble-providers/scrobble-read-status.enum";

export enum ScrobbleEventType {
  ChapterRead = 0,
  AddWantToRead = 1,
  RemoveWantToRead = 2,
  ScoreUpdated = 3,
  Review = 4,
  ReadStatusUpdate = 5,
}

export interface ScrobbleEvent {
  id: number;
  seriesName: string;
  seriesId: number;
  libraryId: number;
  isProcessed: string;
  scrobbleEventType: ScrobbleEventType;
  scrobbleProvider: ScrobbleProvider;
  readStatus: ScrobbleReadStatus;
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
