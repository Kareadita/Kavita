import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {ScrobbleProviderSettings} from "./scrobble-provider-settings";
import {OAuthUpstream} from "../oauth-upstream";

export class UserScrobbleProvider {
  provider!: ScrobbleProvider;
  userName!: string;
  authenticationToken!: string;
  refreshToken?: string;
  validUntilUtc!: string;
  lastSyncedUtc!: string;
  hasRunScrobbleEventGeneration!: boolean;
  scrobbleEventGenerationRan!: string;
  settings!: ScrobbleProviderSettings;

  get generateTokenLink(): string | null {
    switch (this.provider) {
      case ScrobbleProvider.Hardcover:
        return "https://hardcover.app/account/api";
    }

    return null;
  }

  get supportsOAuthFlow() {
    return ![ScrobbleProvider.Hardcover, ScrobbleProvider.Cbr, ScrobbleProvider.Kavita].includes(this.provider);
  }

  get oAuthUpStream() {
    switch (this.provider) {
      case ScrobbleProvider.AniList:
        return OAuthUpstream.AniList;
      case ScrobbleProvider.Mal:
        return OAuthUpstream.MyAnimeList;
      case ScrobbleProvider.MangaBaka:
        return OAuthUpstream.MangaBaka;
    }

    return null;
  }

  get canGenerateEvents(): boolean {
    if (this.provider === ScrobbleProvider.Mal) {
      return false;
    }

    return (this.authenticationToken ?? '') !== '';
  }



  static From(data: Partial<UserScrobbleProvider>): UserScrobbleProvider {
    return Object.assign(new UserScrobbleProvider(), data);
  }
}
