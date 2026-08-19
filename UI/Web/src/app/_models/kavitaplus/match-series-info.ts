import {PlusMediaFormat} from "../series-detail/external-series-detail";
import {LibraryType} from "../library/library";
import {MetadataProvider} from "./metadata-provider.enum";
import {MangaFormat} from "../manga-format";

export interface MatchSeriesInfo {
  hasMatch: boolean;
  /** Dictates there is a Match AND it's AniList */
  isLegacy: boolean;
  plusMediaFormat: PlusMediaFormat;
  libraryType: LibraryType;
  metadataProvider: MetadataProvider;
  seriesFormat: MangaFormat;
  mangaBakaId?: number;
  /** The currently selected MangaBaka edition, if any */
  mangaBakaEditionId?: string;
  hardcoverId?: number;
  /** This is here since pre-v0.9.1 series will only have AniList **/
  aniListId?: number;
  cbrId?: number;
  isStandAlone: boolean;
}
