import {AgeRestriction} from './metadata/age-restriction';
import {Preferences} from './preferences/preferences';

// This interface is only used for login and storing/retrieving JWT from local storage
export interface User {
  username: string;
  token: string;
  refreshToken: string;
  roles: string[];
  preferences: Preferences;
  apiKey: string;
  email: string;
  ageRestriction: AgeRestriction;
  hasRunScrobbleEventGeneration: boolean;
  scrobbleEventGenerationRan: string; // datetime
  owner: UserOwner,
}

export enum UserOwner {
  Native = 0,
  OpenIdConnect = 1,
}

export const UserOwners: UserOwner[] = [UserOwner.Native, UserOwner.OpenIdConnect];
