import {UserReview} from "../_single-module/review-card/user-review";
import {Rating} from "./rating";

export type ChapterDetailPlus = {
  rating: number;
  hasBeenRated: boolean;
  reviews: UserReview[];
  ratings: Rating[];
};
