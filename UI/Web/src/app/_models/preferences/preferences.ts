import {PageLayoutMode} from '../page-layout-mode';
import {SiteTheme} from './site-theme';
import {HighlightSlot} from "../../book-reader/_models/annotations/highlight-slot";

export interface Preferences {

  // Global
  theme: SiteTheme;
  globalPageLayoutMode: PageLayoutMode;
  blurUnreadSummaries: boolean;
  promptForDownloadSize: boolean;
  noTransitions: boolean;
  collapseSeriesRelationships: boolean;
  shareReviews: boolean;
  locale: string;
  bookReaderHighlightSlots: HighlightSlot[];
  colorScapeEnabled: boolean;

  // Kavita+
  aniListScrobblingEnabled: boolean;
  wantToReadSync: boolean;
}

