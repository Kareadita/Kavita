import {KavitaPlusAuditEntry} from './kavita-plus-audit-entry';

export interface KavitaPlusAuditSeriesInfo {
  seriesId: number;
  libraryId: number;
  seriesName: string;
  isMatched: boolean;
  mangaBakaId?: number;
  aniListId?: number;
  malId?: number;
  hardcoverId?: number;
  metronId?: number;
  comicVineId?: string;
  cbrId?: number;
  nextRefreshUtc: string | null;
  lastRefreshedUtc: string | null;
  recentEvents: KavitaPlusAuditEntry[];
}
