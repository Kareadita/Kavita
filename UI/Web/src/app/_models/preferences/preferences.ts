
import { LayoutMode } from 'src/app/manga-reader/_models/layout-mode';
import { BookPageLayoutMode } from '../readers/book-page-layout-mode';
import { PageLayoutMode } from '../page-layout-mode';
import { PageSplitOption } from './page-split-option';
import { ReaderMode } from './reader-mode';
import { ReadingDirection } from './reading-direction';
import { ScalingOption } from './scaling-option';
import { SiteTheme } from './site-theme';
import {WritingStyle} from "./writing-style";

export interface Preferences {
    // Manga Reader
    readingDirection: ReadingDirection;
    scalingOption: ScalingOption;
    pageSplitOption: PageSplitOption;
    readerMode: ReaderMode;
    autoCloseMenu: boolean;
    layoutMode: LayoutMode;
    backgroundColor: string;
    showScreenHints: boolean;
    emulateBook: boolean;
    swipeToPaginate: boolean;

    // Book Reader
    bookReaderMargin: number;
    bookReaderLineSpacing: number;
    bookReaderFontSize: number;
    bookReaderFontFamily: string;
    bookReaderTapToPaginate: boolean;
    bookReaderReadingDirection: ReadingDirection;
    bookReaderWritingStyle: WritingStyle;
    bookReaderThemeName: string;
    bookReaderLayoutMode: BookPageLayoutMode;
    bookReaderImmersiveMode: boolean;

    // Global
    theme: SiteTheme;
    globalPageLayoutMode: PageLayoutMode;
    blurUnreadSummaries: boolean;
    promptForDownloadSize: boolean;
    noTransitions: boolean;
    collapseSeriesRelationships: boolean;
    shareReviews: boolean;
    locale: string;
}

export const readingDirections = [{text: 'left-to-right', value: ReadingDirection.LeftToRight}, {text: 'right-to-left', value: ReadingDirection.RightToLeft}];
export const bookWritingStyles = [{text: 'horizontal', value: WritingStyle.Horizontal}, {text: 'vertical', value: WritingStyle.Vertical}];
export const scalingOptions = [{text: 'automatic', value: ScalingOption.Automatic}, {text: 'fit-to-height', value: ScalingOption.FitToHeight}, {text: 'fit-to-width', value: ScalingOption.FitToWidth}, {text: 'original', value: ScalingOption.Original}];
export const pageSplitOptions = [{text: 'fit-to-screen', value: PageSplitOption.FitSplit}, {text: 'right-to-left', value: PageSplitOption.SplitRightToLeft}, {text: 'left-to-right', value: PageSplitOption.SplitLeftToRight}, {text: 'no-split', value: PageSplitOption.NoSplit}];
export const readingModes = [{text: 'left-to-right', value: ReaderMode.LeftRight}, {text: 'up-to-down', value: ReaderMode.UpDown}, {text: 'webtoon', value: ReaderMode.Webtoon}];
export const layoutModes = [{text: 'single', value: LayoutMode.Single}, {text: 'double', value: LayoutMode.Double}, {text: 'double-manga', value: LayoutMode.DoubleReversed}]; // , {text: 'Double (No Cover)', value: LayoutMode.DoubleNoCover}
export const bookLayoutModes = [{text: 'scroll', value: BookPageLayoutMode.Default}, {text: '1-column', value: BookPageLayoutMode.Column1}, {text: '2-column', value: BookPageLayoutMode.Column2}];
export const pageLayoutModes = [{text: 'cards', value: PageLayoutMode.Cards}, {text: 'list', value: PageLayoutMode.List}];
