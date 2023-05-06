import { AgeRestriction } from '../metadata/age-restriction';
import { Library } from '../library';

export interface Member {
    id: number;
    username: string;
    email: string;
    lastActive: string; // datetime
    created: string; // datetime
    roles: string[];
    libraries: Library[];
    ageRestriction: AgeRestriction;
    isPending: boolean;
}