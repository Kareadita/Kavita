import {Observable} from "rxjs";
import {StreamType} from "./stream-type.enum";
import {CommonStream} from "../common-stream";

export interface DashboardStream extends CommonStream {
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


