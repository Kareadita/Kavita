import { Library } from './library';

export interface Member {
    username: string;
    lastActive: string; // datetime
    created: string; // datetime
    isAdmin: boolean;
    roles: string[]; // TODO: Refactor members to use RBS
    libraries: Library[];
}