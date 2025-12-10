import {ClientInfo} from "../_services/client-info.service";

/**
 * Represents a physical device a client is using to interact with Kavita
 */
export interface ClientDevice {
  id: number;
  friendlyName: string;
  currentClientInfo: ClientInfo;
  firstSeenUtc: string;
  lastSeenUtc: string;
  ownerUsername: string;
  ownerUserId: number;
}
