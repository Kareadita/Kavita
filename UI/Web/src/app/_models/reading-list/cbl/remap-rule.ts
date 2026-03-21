import {LibraryType} from '../../library/library';

export interface RemapRule {
  id: number;
  normalizedCblSeriesName: string;
  cblSeriesName: string;
  cblVolume: string | null;
  cblNumber: string | null;
  seriesId: number;
  volumeId: number | null;
  chapterId: number | null;
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
