import {PublicationStatus} from "../metadata/publication-status";

export interface RateReviewPrompt {
  seriesId: number;
  volumeId: number;
  chapterId: number;
  seriesName: string;
  publicationStatus: PublicationStatus | null;
}
