
import { LayoutMode } from 'src/app/manga-reader/_models/layout-mode';
import { BookPageLayoutMode } from '../readers/book-page-layout-mode';
import { PageLayoutMode } from '../page-layout-mode';
import { PageSplitOption } from './page-split-option';
import { ReaderMode } from './reader-mode';
import { ReadingDirection } from './reading-direction';
import { ScalingOption } from './scaling-option';
import { SiteTheme } from './site-theme';
import {ReadingMode} from "./reading-mode";

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
    bookReaderReadingMode: ReadingMode;
    bookReaderThemeName: string;
    bookReaderLayoutMode: BookPageLayoutMode;
    bookReaderImmersiveMode: boolean;

    // Global
    theme: SiteTheme;
    globalPageLayoutMode: PageLayoutMode;
    blurUnreadSummaries: boolean;
    promptForDownloadSize: boolean;
    noTransitions: boolean;
}

export const readingDirections = [{text: 'Left to Right', value: ReadingDirection.LeftToRight}, {text: 'Right to Left', value: ReadingDirection.RightToLeft}];
export const bookReadingModes = [{text: 'Horizontally', value: ReadingMode.Horizontally}, {text: 'Vertically', value: ReadingMode.Vertically}];
export const scalingOptions = [{text: 'Automatic', value: ScalingOption.Automatic}, {text: 'Fit to Height', value: ScalingOption.FitToHeight}, {text: 'Fit to Width', value: ScalingOption.FitToWidth}, {text: 'Original', value: ScalingOption.Original}];
export const pageSplitOptions = [{text: 'Fit to Screen', value: PageSplitOption.FitSplit}, {text: 'Right to Left', value: PageSplitOption.SplitRightToLeft}, {text: 'Left to Right', value: PageSplitOption.SplitLeftToRight}, {text: 'No Split', value: PageSplitOption.NoSplit}];
export const readingModes = [{text: 'Left to Right', value: ReaderMode.LeftRight}, {text: 'Up to Down', value: ReaderMode.UpDown}, {text: 'Webtoon', value: ReaderMode.Webtoon}];
export const layoutModes = [{text: 'Single', value: LayoutMode.Single}, {text: 'Double', value: LayoutMode.Double}, {text: 'Double (Manga)', value: LayoutMode.DoubleReversed}]; // , {text: 'Double (No Cover)', value: LayoutMode.DoubleNoCover}
export const bookLayoutModes = [{text: 'Scroll', value: BookPageLayoutMode.Default}, {text: '1 Column', value: BookPageLayoutMode.Column1}, {text: '2 Column', value: BookPageLayoutMode.Column2}];
export const pageLayoutModes = [{text: 'Cards', value: PageLayoutMode.Cards}, {text: 'List', value: PageLayoutMode.List}];
