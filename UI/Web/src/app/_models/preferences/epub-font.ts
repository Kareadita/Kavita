/**
 * Where does the font come from
 */
export enum FontProvider {
  System = 1,
  User = 2,
}

/**
 * Font used in the book reader
 */
export interface EpubFont {
  id: number;
  name: string;
  provider: FontProvider;
  fileName: string;
}
