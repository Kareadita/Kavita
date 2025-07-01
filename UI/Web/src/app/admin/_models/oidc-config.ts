import {AgeRating} from "../../_models/metadata/age-rating";

export interface OidcConfig {
  authority: string;
  clientId: string;
  provisionAccounts: boolean;
  requireVerifiedEmail: boolean;
  syncUserSettings: boolean;
  autoLogin: boolean;
  disablePasswordAuthentication: boolean;
  providerName: string;
  defaultRoles: string[];
  defaultLibraries: number[];
  defaultAgeRating: AgeRating;
  defaultIncludeUnknowns: boolean;
}

export interface OidcPublicConfig {
  authority: string;
  clientId: string;
  autoLogin: boolean;
  disablePasswordAuthentication: boolean;
  providerName: string;
}
