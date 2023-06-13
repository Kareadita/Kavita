export interface UserReview {
  seriesId: number;
  libraryId: number;
  score: number;
  username: string;
  body: string;
  tagline?: string;
}
