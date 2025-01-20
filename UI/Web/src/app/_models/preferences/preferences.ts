
import { LayoutMode } from 'src/app/manga-reader/_models/layout-mode';
import { BookPageLayoutMode } from '../readers/book-page-layout-mode';
import { PageLayoutMode } from '../page-layout-mode';
import { PageSplitOption } from './page-split-option';
import { ReaderMode } from './reader-mode';
import { ReadingDirection } from './reading-direction';
import { ScalingOption } from './scaling-option';
import { SiteTheme } from './site-theme';
import {WritingStyle} from "./writing-style";
import {PdfTheme} from "./pdf-theme";
import {PdfScrollMode} from "./pdf-scroll-mode";
import {PdfLayoutMode} from "./pdf-layout-mode";
import {PdfSpreadMode} from "./pdf-spread-mode";

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

    // PDF Reader
    pdfTheme: PdfTheme;
    pdfScrollMode: PdfScrollMode;
    pdfSpreadMode: PdfSpreadMode;

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

export const readingDirections = [{text: 'left-to-right', value: ReadingDirection.LeftToRight}, {text: 'right-to-left', value: ReadingDirection.RightToLeft}];
export const bookWritingStyles = [{text: 'horizontal', value: WritingStyle.Horizontal}, {text: 'vertical', value: WritingStyle.Vertical}];
export const scalingOptions = [{text: 'automatic', value: ScalingOption.Automatic}, {text: 'fit-to-height', value: ScalingOption.FitToHeight}, {text: 'fit-to-width', value: ScalingOption.FitToWidth}, {text: 'original', value: ScalingOption.Original}];
export const pageSplitOptions = [{text: 'fit-to-screen', value: PageSplitOption.FitSplit}, {text: 'right-to-left', value: PageSplitOption.SplitRightToLeft}, {text: 'left-to-right', value: PageSplitOption.SplitLeftToRight}, {text: 'no-split', value: PageSplitOption.NoSplit}];
export const readingModes = [{text: 'left-to-right', value: ReaderMode.LeftRight}, {text: 'up-to-down', value: ReaderMode.UpDown}, {text: 'webtoon', value: ReaderMode.Webtoon}];
export const layoutModes = [{text: 'single', value: LayoutMode.Single}, {text: 'double', value: LayoutMode.Double}, {text: 'double-manga', value: LayoutMode.DoubleReversed}]; // TODO: Build this, {text: 'Double (No Cover)', value: LayoutMode.DoubleNoCover}
export const bookLayoutModes = [{text: 'scroll', value: BookPageLayoutMode.Default}, {text: '1-column', value: BookPageLayoutMode.Column1}, {text: '2-column', value: BookPageLayoutMode.Column2}];
export const pageLayoutModes = [{text: 'cards', value: PageLayoutMode.Cards}, {text: 'list', value: PageLayoutMode.List}];
export const pdfLayoutModes = [{text: 'pdf-multiple', value: PdfLayoutMode.Multiple},  {text: 'pdf-book', value: PdfLayoutMode.Book}];
export const pdfScrollModes = [{text: 'pdf-vertical', value: PdfScrollMode.Vertical}, {text: 'pdf-horizontal', value: PdfScrollMode.Horizontal}, {text: 'pdf-page', value: PdfScrollMode.Page}];
export const pdfSpreadModes = [{text: 'pdf-none', value: PdfSpreadMode.None}, {text: 'pdf-odd', value: PdfSpreadMode.Odd}, {text: 'pdf-even', value: PdfSpreadMode.Even}];
export const pdfThemes = [{text: 'pdf-light', value: PdfTheme.Light}, {text: 'pdf-dark', value: PdfTheme.Dark}];
