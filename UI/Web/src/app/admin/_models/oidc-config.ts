import {AgeRating} from "../../_models/metadata/age-rating";

export interface OidcPublicConfig {
  autoLogin: boolean;
  disablePasswordAuthentication: boolean;
  providerName: string;
  enabled: boolean;
}

export interface OidcConfig extends OidcPublicConfig {
  authority: string;
  clientId: string;
  secret: string;
  provisionAccounts: boolean;
  requireVerifiedEmail: boolean;
  syncUserSettings: boolean;
  rolesPrefix: string;
  rolesClaim: string;
  customScopes: string[];
  defaultRoles: string[];
  defaultLibraries: number[];
  defaultAgeRestriction: AgeRating;
  defaultIncludeUnknowns: boolean;
}

export enum AuthorityValidationResult {
  Success = 0,
  InvalidAuthority = 1,
  Failure = 2,
  NotApplicable = 3,
  MissingHttps = 4,
}
