import {AgeRestriction} from './metadata/age-restriction';
import {Preferences} from './preferences/preferences';

// This interface is only used for login and storing/retrieving JWT from local storage
export interface User {
  username: string;
  // This is set by the oidc service, will always take precedence over the Kavita generated token
  // When set, the refresh logic for the Kavita token will not run
  oidcToken: string;
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
