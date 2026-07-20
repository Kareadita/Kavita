import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {AgeRating} from "../metadata/age-rating";

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

export enum EditionEntryType {
  Chapter = 0,
  Volume = 1,
  Other = 2,
}

export interface ExternalEditionDto {
  id: string;
  title: string;
  format: string;
  language: string;
  publisher: string;
  type: EditionEntryType;
  /**
   * Number of entries in the main storyline
   */
  mainCount: number;
  /**
   * Total number of entries (includes extras, etc.)
   */
  totalCount: number;
}

export interface ExternalSeriesDetail {
  name: string;
  aniListId?: number | null;
  malId?: number | null;
  mangabakaId?: number | null;
  hardcoverId?: number | null;
  isStandAlone: boolean;
  cbrId?: number | null;
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
  startDate?: string | null;
  endDate?: string | null;
  averageScore?: number | null;
  staff: Array<SeriesStaff>;
  tags: Array<MetadataTagDto>;
  provider: ScrobbleProvider;
  ageRating: AgeRating;
  editions: ExternalEditionDto[];
}
