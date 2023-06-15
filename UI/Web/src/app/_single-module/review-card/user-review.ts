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
}
