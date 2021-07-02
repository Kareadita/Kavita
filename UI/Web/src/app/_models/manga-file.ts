import { MangaFormat } from './manga-format';

export interface MangaFile {
    filePath: string;
    pages: number;
    format: MangaFormat;
}
