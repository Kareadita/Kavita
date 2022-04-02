/**
 * How the image should scale to the screen size
 */
export enum ScalingOption {
    /**
     * Fit the image into the height of screen
     */
    FitToHeight = 0,
    /**
     * Fit the image into the width of screen
     */
    FitToWidth = 1,
    /**
     * Apply no logic and render the image as is
     */
    Original = 2,
    /**
     * Ask the reader to attempt to choose the best ScalingOption for the user
     */
    Automatic = 3
}
