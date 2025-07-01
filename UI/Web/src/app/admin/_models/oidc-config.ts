
export interface OidcConfig {
  authority: string;
  clientId: string;
  provisionAccounts: boolean;
  requireVerifiedEmail: boolean;
  syncUserSettings: boolean;
  autoLogin: boolean;
  disablePasswordAuthentication: boolean;
  providerName: string;
}

export interface OidcPublicConfig {
  authority: string;
  clientId: string;
  autoLogin: boolean;
  disablePasswordAuthentication: boolean;
  providerName: string;
}
