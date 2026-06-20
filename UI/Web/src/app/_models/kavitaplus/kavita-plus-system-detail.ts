import {ScrobbleProvider} from "../../_services/scrobbling.service";

export interface KavitaPlusSystemDetail {
  provider: ScrobbleProvider;
  validUntilUtc: string | null;
  userInfo: KavitaPlusUserInfo | null;
}

export interface KavitaPlusUserInfo {
  username: string;
}
