import {ScrobbleProvider} from "../../_services/scrobbling.service";

export enum PlusMediaFormat {
  Manga = 1,
  Comic = 2,
  LightNovel = 3,
  Book = 4
}

export interface SeriesStaff {
  name: string;
  url: string;
  role: string;
  imageUrl?: string;
  gender?: string;
  description?: string;
}

export interface MetadataTagDto {
  name: string;
  description: string;
  rank?: number;
  isGeneralSpoiler: boolean;
  isMediaSpoiler: boolean;
  isAdult: boolean;
}

export interface ExternalSeriesDetail {
  name: string;
  aniListId?: number;
  malId?: number;
  synonyms: Array<string>;
  plusMediaFormat: PlusMediaFormat;
  siteUrl?: string;
  coverUrl?: string;
  genres: Array<string>;
  summary?: string;
  volumeCount?: number;
  chapterCount?: number;
  /**
   * These are duplicated with volumeCount based on where it's being invoked.
   */
  volumes?: number;
  chapters?: number;
  staff: Array<SeriesStaff>;
  tags: Array<MetadataTagDto>;
  provider: ScrobbleProvider;
}
