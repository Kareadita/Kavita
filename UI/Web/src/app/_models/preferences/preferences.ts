import {PageLayoutMode} from '../page-layout-mode';
import {SiteTheme} from './site-theme';

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

  // Kavita+
  aniListScrobblingEnabled: boolean;
  wantToReadSync: boolean;
}

