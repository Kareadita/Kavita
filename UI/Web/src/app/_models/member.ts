import { Library } from './library';

export interface Member {
    id: number;
    username: string;
    email: string;
    lastActive: string; // datetime
    created: string; // datetime
    isAdmin: boolean;
    roles: string[];
    libraries: Library[];
}