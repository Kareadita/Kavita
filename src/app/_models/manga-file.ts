import { MangaFormat } from './manga-format';

export interface MangaFile {
    filePath: string;
    //chapter: number;
    numberOfPages: number;
    format: MangaFormat;
}
