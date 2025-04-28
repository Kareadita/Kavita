import {UserReview} from "../_single-module/review-card/user-review";
import {Rating} from "./rating";

export type ChapterDetail = {
  rating: number;
  hasBeenRated: boolean;
  reviews: UserReview[];
  ratings: Rating[];
};
