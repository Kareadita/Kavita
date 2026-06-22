import {AgeRating} from "../../metadata/age-rating";
import {ReviewScrobbleTarget} from "./review-scrobble-target.enum";
import {ReadStatusTransitionRule} from "./read-status-transition-rule";

export type ScrobbleProviderSettings = {
  progressScrobbling: boolean;
  wantToReadSync: boolean;
  ratingScrobbling: boolean;
  reviewsScrobbling: boolean;
  reviewScrobbleTarget: ReviewScrobbleTarget;
  allLibraries: boolean;
  libraries: number[];
  highestAgeRating: AgeRating;
  inactiveSeriesRule: ReadStatusTransitionRule;
  droppedSeriesRule: ReadStatusTransitionRule;
}
