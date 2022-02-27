import { ThemeProvider } from "./site-theme";

/**
 * Theme for the the book reader contents
 */
 export interface BookTheme {
    id: number;
    name: string;
    provider: ThemeProvider;
    /**
     * Main color (usually background color) that represents the theme
     */
    colorHash: string;
    /**
     * Used to identify the theme on style tag
     */
    selector: string;
  }
