/**
 * Where does the theme come from
 */
 export enum ThemeProvider {
    System = 1,
    Custom = 2,
  }

  /**
   * Theme for the whole instance
   */
  export interface SiteTheme {
    id: number;
    name: string;
    normalizedName: string;
    filePath: string;
    isDefault: boolean;
    provider: ThemeProvider;
    /**
     * The actual class the root is defined against. It is generated at the backend.
     */
    selector: string;
    description: string;
    previewUrls: Array<string>;
    author: string;

  }
