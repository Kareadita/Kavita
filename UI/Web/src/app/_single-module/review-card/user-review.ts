import {ScrobbleProvider} from "../../_services/scrobbling.service";

export interface UserReview {
  seriesId: number;
  libraryId: number;
  volumeId?: number;
  chapterId?: number;
  rating: number;
  hasBeenRated: boolean;
  score: number;
  username: string;
  body: string;
  tagline?: string;
  isExternal: boolean;
  bodyJustText?: string;
  siteUrl?: string;
  provider: ScrobbleProvider;
}
