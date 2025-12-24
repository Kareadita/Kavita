import {FileTypeGroup} from "./library/file-type-group.enum";

export enum MangaFormat {
    IMAGE = 0,
    ARCHIVE = 1,
    UNKNOWN = 2,
    EPUB = 3,
    PDF = 4
}

export const allMangaFormats= Object.keys(MangaFormat)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as MangaFormat[];
