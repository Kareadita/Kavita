import {ScrobbleProvider} from "../../_services/scrobbling.service";

export interface UserReview {
  seriesId: number;
  libraryId: number;
  score: number;
  username: string;
  body: string;
  tagline?: string;
  isExternal: boolean;
  bodyJustText?: string;
  externalUrl?: string;
  provider: ScrobbleProvider;
}
