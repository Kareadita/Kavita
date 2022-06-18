/**
 * A font family to inject into the book reader
 */
export interface ThemeFont {
    /**
     * Name/Font-family
     */
    fontFamily: string;
    /**
     * Where the font is loaded from?
     */
    fontSrc: string;
    format: 'truetype';

}