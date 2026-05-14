import {KavitaPlusAuditEntry} from './kavita-plus-audit-entry';

export interface KavitaPlusAuditSeriesInfo {
  seriesId: number;
  seriesName: string;
  isMatched: boolean;
  mangaBakaId: number | null;
  lastRefreshedUtc: string | null;
  recentEvents: KavitaPlusAuditEntry[];
}
