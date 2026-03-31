import {LibraryType} from '../../library/library';
import {CblRemapRuleKind} from './cbl-remap-rule-kind.enum';

export interface RemapRule {
  id: number;
  normalizedCblSeriesName: string;
  cblSeriesName: string;
  cblVolume: string | null;
  cblNumber: string | null;
  seriesId: number;
  volumeId: number | null;
  volumeNumber: string;
  chapterId: number | null;
  kind: CblRemapRuleKind;
  chapterRange: string;
  chapterTitleName: string;
  chapterIsSpecial: boolean;
  libraryType: LibraryType;
  seriesNameAtMapping: string;
  appUserId: number;
  isGlobal: boolean;
  createdByUserName: string;
  createdUtc: string;
}
