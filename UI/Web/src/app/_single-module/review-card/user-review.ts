import {ScrobbleProvider} from "../../_services/scrobbling.service";

export enum RatingAuthority {
  User = 0,
  Critic = 1
}

export interface UserReview {
  seriesId: number;
  libraryId: number;
  chapterId?: number;
  score: number;
  username: string;
  body: string;
  tagline?: string;
  isExternal: boolean;
  bodyJustText?: string;
  siteUrl?: string;
  provider: ScrobbleProvider;
  authority: RatingAuthority;
}
