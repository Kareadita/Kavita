import {ScrobbleProvider} from "../_services/scrobbling.service";
import {RatingAuthority} from "./rating";
import {Chapter} from "./chapter";
import {Series} from "./series";
import {Person} from "./metadata/person";


export interface UserReview {
  seriesId: number;
  libraryId: number;
  chapterId?: number;
  score: number;
  username: string;
  userId: number;
  body: string;
  tagline?: string;
  isExternal: boolean;
  bodyJustText?: string;
  siteUrl?: string;
  provider: ScrobbleProvider;
  authority: RatingAuthority;
}

export interface UserReviewExtended {
  id: number;
  /**
   * The main review
   */
  body: string;
  /**
   * The series this is for
   */
  seriesId: number;
  /**
   * The chapter this is for (optional - null for series-level reviews)
   */
  chapterId?: number;
  /**
   * The library this series belongs in
   */
  libraryId: number;
  /**
   * The user who wrote this
   */
  username: string;
  /**
   * Rating value (0-5 scale typically)
   */
  rating: number;
  /**
   * The series information
   */
  series: Series;
  /**
   * The chapter information (optional - null for series-level reviews)
   */
  chapter?: Chapter;
  /**
   * Date the review/rating was made
   */
  createdUtc: string;
  writers: Person[];
}
