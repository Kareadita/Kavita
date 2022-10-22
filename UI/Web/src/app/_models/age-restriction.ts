import { AgeRating } from "./metadata/age-rating";

export interface AgeRestriction {
    ageRating: AgeRating;
    includeUnknowns: boolean;
}