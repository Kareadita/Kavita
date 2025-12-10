import {Observable} from "rxjs";
import {StreamType} from "./stream-type.enum";
import {CommonStream} from "../common-stream";
import {FilterV2} from "../metadata/v2/filter-v2";

export interface DashboardStream extends CommonStream {
  id: number;
  name: string;
  isProvided: boolean;
  api: Observable<any[]>;
  smartFilterId: number;
  smartFilterEncoded?: string;
  smartFilterDecoded?: FilterV2,
  streamType: StreamType;
  order: number;
  visible: boolean;
}


