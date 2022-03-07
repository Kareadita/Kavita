/**
 * The pagination method used by the reader
 */
export enum ReaderMode {
    /**
     * Manga default left/right to page
     */
    LeftRight = 0,
    /**
     * Manga up and down to page
     */
    UpDown = 1,
    /**
     * Webtoon reading (scroll) with optional areas to tap
     */
    Webtoon = 2
}