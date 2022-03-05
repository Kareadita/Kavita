/**
 * How to layout pages for reading
 */
export enum LayoutMode {
    /**
     * Renders a single page on the renderer. Cover images will follow splitting logic.
     */
    Single = 'Single',
    /**
     * Renders 2 pages side by side on the renderer. Cover images will not split and take up both panes.
     */
    Double = 'Double', 

}