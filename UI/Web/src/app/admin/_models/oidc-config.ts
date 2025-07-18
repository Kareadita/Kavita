import {AgeRating} from "../../_models/metadata/age-rating";

export interface OidcPublicConfig {
  authority: string;
  clientId: string;
  autoLogin: boolean;
  disablePasswordAuthentication: boolean;
  providerName: string;
}

export interface OidcConfig extends OidcPublicConfig {
  provisionAccounts: boolean;
  requireVerifiedEmail: boolean;
  syncUserSettings: boolean;
  defaultRoles: string[];
  defaultLibraries: number[];
  defaultAgeRestriction: AgeRating;
  defaultIncludeUnknowns: boolean;
}

