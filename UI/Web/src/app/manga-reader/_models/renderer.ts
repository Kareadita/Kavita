import { Observable } from "rxjs";

/**
 * A generic interface for an image renderer 
 */
export interface ImageRenderer {

    /**
     * The current Image 
     */
    image: Observable<HTMLImageElement | null>;
    /**
     * Performs a rendering pass
     */
    renderPage(): void;
    /**
     * If a valid move next page should occur, this will return true. Otherwise, this will return false. 
     */
    shouldMovePrev(): boolean;
    /**
     * If a valid move prev page should occur, this will return true. Otherwise, this will return false. 
     */
    shouldMoveNext(): boolean;
    /**
     * Returns the number of pages that should occur based on page direction and internal state of the renderer.
     */
    getPageAmount(): number;
    /**
     * When layout shifts occur, where a re-render might be needed but from menu option (like split option changed on a split image), this will be called.
     * This should reset any needed state, but not unset the image.
     */
    reset(): void;
    

}