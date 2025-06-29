
export interface OidcConfig {
  authority: string;
  clientId: string;
  provisionAccounts: boolean;
  requireVerifiedEmail: boolean;
  provisionUserSettings: boolean;
  autoLogin: boolean;
}

export interface OidcPublicConfig {
  authority: string;
  clientId: string;
  autoLogin: boolean;
}
