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
  family: string;
  name: string;
  provider: FontProvider;
  fileName: string;
  style: string;
  weight: string;
}

/**
 * Result of attempting to delete a font family
 */
export interface FontDeleteResult {
  /** True when the family was removed */
  deleted: boolean;
  /** True when the family is currently selected by one or more users */
  inUse: boolean;
}
