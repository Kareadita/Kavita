import {PageLayoutMode} from '../page-layout-mode';
import {SiteTheme} from './site-theme';
import {HighlightSlot} from "../../book-reader/_models/annotations/highlight-slot";
import {AgeRating} from "../metadata/age-rating";
import {KeyCode} from "../../_services/key-bind.service";

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
  dataSaver: boolean;
  promptForRereadsAfter: number;
  customKeyBinds: Partial<Record<KeyBindTarget, KeyBind[]>>;

  // Kavita+
  aniListScrobblingEnabled: boolean;
  wantToReadSync: boolean;

  // Social
  socialPreferences: SocialPreferences;

  opdsPreferences: OpdsPreferences;
}

export interface SocialPreferences {
  shareReviews: boolean;
  shareAnnotations: boolean;
  viewOtherAnnotations: boolean;
  socialLibraries: number[];
  socialMaxAgeRating: AgeRating;
  socialIncludeUnknowns: boolean;
  shareProfile: boolean;
}

export interface OpdsPreferences {
  embedProgressIndicator: boolean;
  includeContinueFrom: boolean;
}

export interface KeyBind {
  meta?: boolean;
  control?: boolean;
  alt?: boolean;
  shift?: boolean;
  controllerSequence?: readonly string[];
  key: KeyCode;
}

export enum KeyBindTarget {
  NavigateToSettings = 'NavigateToSettings',
  OpenSearch = 'OpenSearch',
  NavigateToScrobbling = 'NavigateToScrobbling',

  ToggleFullScreen = 'ToggleFullScreen',
  BookmarkPage = 'BookmarkPage',
  OpenHelp = 'OpenHelp',
  GoTo = "GoTo",
  ToggleMenu = 'ToggleMenu',
  PageLeft = 'PageLeft',
  PageRight = 'PageRight',
  Escape = 'Escape',
}

export interface OpdsPreferences {
  embedProgressIndicator: boolean;
  includeContinueFrom: boolean;
}

