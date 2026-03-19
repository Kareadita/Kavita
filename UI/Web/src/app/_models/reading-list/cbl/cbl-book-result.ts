import {CblImportReason} from './cbl-import-reason.enum';
import {CblMatchTier} from './cbl-match-tier';
import {CblSeriesCandidate} from './cbl-series-candidate';

export interface CblBookResult {
  order: number;
  series: string;
  volume: string;
  number: string;
  /**
   * For SeriesCollision
   */
  libraryId: number;
  /**
   * For SeriesCollision
   */
  seriesId: number;
  readingListName: string;
  reason: CblImportReason;
  matchTier: CblMatchTier | null;
  chapterId: number;
  chapterTitle: string;
  candidates: CblSeriesCandidate[];
}
