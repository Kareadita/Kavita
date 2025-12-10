import {Recommendation} from "./recommendation";
import {UserReview} from "../user-review";
import {Rating} from "../rating";

export interface SeriesDetailPlus {
  recommendations?: Recommendation;
  reviews: Array<UserReview>;
  ratings?: Array<Rating>;
}
