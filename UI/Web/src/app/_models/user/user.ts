import {AgeRestriction} from '../metadata/age-restriction';
import {Preferences} from '../preferences/preferences';
import {IHasCover} from "../common/i-has-cover";
import {AuthKey} from "./auth-key";

// This interface is only used for login and storing/retrieving JWT from local storage
export interface User extends IHasCover {
  id: number;
  username: string;
  token: string;
  refreshToken: string;
  roles: string[];
  preferences: Preferences;
  // ApiKey is deprecated in favor of AuthKeys
  //apiKey: string;
  email: string;
  ageRestriction: AgeRestriction;
  hasRunScrobbleEventGeneration: boolean;
  scrobbleEventGenerationRan: string; // datetime
  identityProvider: IdentityProvider;
  authKeys: AuthKey[];

  coverImage?: string;
  primaryColor: string;
  secondaryColor: string;
}

export enum IdentityProvider {
  Kavita = 0,
  OpenIdConnect = 1,
}

export const IdentityProviders: IdentityProvider[] = [IdentityProvider.Kavita, IdentityProvider.OpenIdConnect];
