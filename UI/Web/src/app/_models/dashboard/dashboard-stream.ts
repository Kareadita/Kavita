import {Observable} from "rxjs";
import {StreamType} from "./stream-type.enum";

export interface DashboardStream {
  id: number;
  name: string;
  isProvided: boolean;
  api: Observable<any[]>;
  smartFilterId: number;
  smartFilterEncoded?: string;
  streamType: StreamType;
  order: number;
  visible: boolean;
}
