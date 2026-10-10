import {MediaErrorReason} from '../../admin/_models/media-error';

export interface ScanIssueSummaryItem {
  filePath: string;
  reason: MediaErrorReason;
  seriesId: number | null;
  seriesName: string | null;
}
