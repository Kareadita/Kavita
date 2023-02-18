import { AgeRating } from "./age-rating";

export interface AgeRestriction {
    ageRating: AgeRating;
    includeUnknowns: boolean;
}