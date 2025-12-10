import {ClientDeviceType} from "../../_services/client-info.service";
import {StatCount} from "./stat-count";

export interface ClientDeviceBreakdown {
  records: Array<StatCount<ClientDeviceType>>;
  totalCount: number;
}
