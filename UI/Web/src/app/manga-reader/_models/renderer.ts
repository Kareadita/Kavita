import { Observable } from "rxjs";

/**
 * A generic interface for an image renderer 
 */
export interface ImageRenderer {

    /**
     * The current Image 
     */
    image: Observable<HTMLImageElement>;
    render(): void;
    

}