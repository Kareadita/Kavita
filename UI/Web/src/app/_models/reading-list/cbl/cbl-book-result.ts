import {CblImportReason} from './cbl-import-reason.enum';
import {CblMatchTier} from './cbl-match-tier';
import {CblSeriesCandidate} from './cbl-series-candidate';
import {LibraryType} from '../../library/library';

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
  matchedSeriesName: string;
  libraryType: LibraryType;
  chapterNumber: string;
  candidates: CblSeriesCandidate[];
}
