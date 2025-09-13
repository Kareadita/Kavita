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
  identityProvider: IdentityProvider,
}

export enum IdentityProvider {
  Kavita = 0,
  OpenIdConnect = 1,
}

export const IdentityProviders: IdentityProvider[] = [IdentityProvider.Kavita, IdentityProvider.OpenIdConnect];
