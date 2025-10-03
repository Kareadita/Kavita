import {PageLayoutMode} from '../page-layout-mode';
import {SiteTheme} from './site-theme';
import {HighlightSlot} from "../../book-reader/_models/annotations/highlight-slot";
import {AgeRating} from "../metadata/age-rating";

export interface Preferences {

  // Global
  theme: SiteTheme;
  globalPageLayoutMode: PageLayoutMode;
  blurUnreadSummaries: boolean;
  promptForDownloadSize: boolean;
  noTransitions: boolean;
  collapseSeriesRelationships: boolean;
  locale: string;
  bookReaderHighlightSlots: HighlightSlot[];
  colorScapeEnabled: boolean;

  // Kavita+
  aniListScrobblingEnabled: boolean;
  wantToReadSync: boolean;

  // Social
  socialPreferences: SocialPreferences;
}

export interface SocialPreferences {
  shareReviews: boolean;
  shareAnnotations: boolean;
  viewOtherAnnotations: boolean;
  socialLibraries: number[];
  socialMaxAgeRating: AgeRating;
  socialIncludeUnknowns: boolean;
}

