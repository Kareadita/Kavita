import {Recommendation} from "./recommendation";
import {UserReview} from "../../_single-module/review-card/user-review";
import {Rating} from "../rating";

export interface SeriesDetailPlus {
  recommendations?: Recommendation;
  reviews: Array<UserReview>;
  ratings?: Array<Rating>;
}
