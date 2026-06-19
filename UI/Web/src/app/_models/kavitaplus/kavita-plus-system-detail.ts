import {ScrobbleProvider} from "../../_services/scrobbling.service";

export interface KavitaPlusSystemDetail {
  provider: ScrobbleProvider;
  validUntilUtc: string;
  userInfo: KavitaPlusUserInfo;
}

export interface KavitaPlusUserInfo {
  username: string;
}
