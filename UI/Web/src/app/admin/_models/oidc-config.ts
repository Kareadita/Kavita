import {AgeRating} from "../../_models/metadata/age-rating";

export interface OidcPublicConfig {
  authority: string;
  clientId: string;
  autoLogin: boolean;
  disablePasswordAuthentication: boolean;
  providerName: string;
  customScopes: string[];
}

export interface OidcConfig extends OidcPublicConfig {
  secret: string;
  provisionAccounts: boolean;
  requireVerifiedEmail: boolean;
  syncUserSettings: boolean;
  rolesPrefix: string;
  rolesClaim: string;
  defaultRoles: string[];
  defaultLibraries: number[];
  defaultAgeRestriction: AgeRating;
  defaultIncludeUnknowns: boolean;
}

