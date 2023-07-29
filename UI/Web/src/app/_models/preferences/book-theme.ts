import { ThemeProvider } from "./site-theme";

/**
 * Theme for the the book reader contents
 */
 export interface BookTheme {
    name: string;
    provider: ThemeProvider;
    /**
     * Main color (usually background color) that represents the theme
     */
    colorHash: string;
    isDefault: boolean;
    /**
     * Is this theme providing dark mode to the reader aka Should we style the reader controls to be dark mode
     */
    isDarkTheme: boolean;
    /**
     * Used to identify the theme on style tag
     */
    selector: string;
    /**
     * Inner HTML
     */
    content: string;
    /**
     * Key for translation
     */
    translationKey: string;
  }
