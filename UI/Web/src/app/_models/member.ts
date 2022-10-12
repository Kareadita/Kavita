import { Library } from './library';
import { AgeRating } from './metadata/age-rating';

export interface Member {
    id: number;
    username: string;
    email: string;
    lastActive: string; // datetime
    created: string; // datetime
    roles: string[];
    libraries: Library[];
    /**
     * If not applicable, will store a -1
     */
    ageRestriction: AgeRating;
}