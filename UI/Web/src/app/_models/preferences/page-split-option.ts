export enum PageSplitOption {
    /**
     * Renders the left side of the image then the right side
     */
    SplitLeftToRight = 0,
    /**
     * Renders the right side of the image then the left side
     */
    SplitRightToLeft = 1,
    /**
     * Don't split and show the image in original size
     */
    NoSplit = 2,
    /**
     * Don't split and scale the image to fit screen space
     */
    FitSplit = 3
}
