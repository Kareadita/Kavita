import { PageSplitOption } from './page-split-option';
import { READER_MODE } from './reader-mode';
import { ReadingDirection } from './reading-direction';
import { ScalingOption } from './scaling-option';

export interface Preferences {
    // Manga Reader
    readingDirection: ReadingDirection;
    scalingOption: ScalingOption;
    pageSplitOption: PageSplitOption;
    readerMode: READER_MODE;
    autoCloseMenu: boolean;
    
    // Book Reader
    bookReaderDarkMode: boolean;
    bookReaderMargin: number;
    bookReaderLineSpacing: number;
    bookReaderFontSize: number;
    bookReaderFontFamily: string;
    bookReaderTapToPaginate: boolean;
    bookReaderReadingDirection: ReadingDirection;

    // Global
    siteDarkMode: boolean;
}

export const readingDirections = [{text: 'Left to Right', value: ReadingDirection.LeftToRight}, {text: 'Right to Left', value: ReadingDirection.RightToLeft}];
export const scalingOptions = [{text: 'Automatic', value: ScalingOption.Automatic}, {text: 'Fit to Height', value: ScalingOption.FitToHeight}, {text: 'Fit to Width', value: ScalingOption.FitToWidth}, {text: 'Original', value: ScalingOption.Original}];
export const pageSplitOptions = [{text: 'Right to Left', value: PageSplitOption.SplitRightToLeft}, {text: 'Left to Right', value: PageSplitOption.SplitLeftToRight}, {text: 'No Split', value: PageSplitOption.NoSplit}];
export const readingModes = [{text: 'Left to Right', value: READER_MODE.MANGA_LR}, {text: 'Up to Down', value: READER_MODE.MANGA_UD}/*, {text: 'Webtoon', value: READER_MODE.WEBTOON}*/];
