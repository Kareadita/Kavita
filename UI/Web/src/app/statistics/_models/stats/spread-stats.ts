import {StatBucket} from "./stat-bucket";

export interface SpreadStats {
  buckets: Array<StatBucket>;
  totalCount: number;
}
