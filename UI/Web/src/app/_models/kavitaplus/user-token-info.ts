import {ScrobbleProvider} from "../../_services/scrobbling.service";

export interface UserTokenInfo {
  userId: number;
  username: string;
  tokens: TokenValidityInfo[];
}

export interface TokenValidityInfo {
  provider: ScrobbleProvider;
  validUntilUtc: string;
}
