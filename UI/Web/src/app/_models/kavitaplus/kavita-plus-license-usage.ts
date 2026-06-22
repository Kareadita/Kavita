import {KavitaPlusApiName} from "./kavita-plus-api-name.enum";

export interface KavitaPlusLicenseUsage {
  generatedAtUtc: string;
  stats: ApiUsage[];
}

export interface ApiUsage {
  apiName: KavitaPlusApiName;
  lifetimeCount: number;
  last30DaysCount: number;
  dailyBuckets: DailyBucket[];
}

export interface DailyBucket {
  /** DateOnly **/
  date: string;
  count: number;
}
