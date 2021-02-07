import { Preferences } from './preferences/preferences';

// This interface is only used for login and storing/retreiving JWT from local storage
export interface User {
    username: string;
    token: string;
    roles: string[];
    preferences: Preferences;
}