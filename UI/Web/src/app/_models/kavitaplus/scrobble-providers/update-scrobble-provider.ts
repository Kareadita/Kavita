import {ScrobbleProvider} from "../../../_services/scrobbling.service";

export type UpdateScrobbleProvider = {
  provider: ScrobbleProvider;
  userName: string;
  authenticationToken: string;
}
