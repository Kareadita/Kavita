import { PAGING_DIRECTION } from "./reader-enums";


/**
* Bitwise enums for configuring how much debug information we want
*/
export const enum DEBUG_MODES {
 /**
  * No Debug information
  */
 None = 0,
 /**
  * Turn on debug logging
  */
 Logs = 2,
}

/**
 * A generic interface for an image renderer 
 */
export interface ImageRenderer {

    /**
     * Performs a rendering pass. This is passed one or more images to render from prefetcher
     */
    renderPage(img: Array<HTMLImageElement | null>): void;
    /**
     * Returns the number of pages that should occur based on page direction and internal state of the renderer. Should return 0 when not in the conditions to render.
     */
    getPageAmount(direction: PAGING_DIRECTION): number;
    /**
     * When layout shifts occur, where a re-render might be needed but from menu option (like split option changed on a split image), this will be called.
     * This should reset any needed state, but not unset the image.
     */
    reset(): void;
    /**
     * Returns the number of pages that are currently rendererd on screen and thus should be bookmarked.
     */
    getBookmarkPageCount(): number;
}