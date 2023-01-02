import { MangaFormat } from './manga-format';

export interface MangaFile {
    id: number;
    filePath: string;
    pages: number;
    format: MangaFormat;
    created: string;
    bytes: number;
}
