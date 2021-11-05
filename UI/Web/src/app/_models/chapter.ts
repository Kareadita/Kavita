import { MangaFile } from './manga-file';

export interface Chapter {
    id: number;
    range: string;
    number: string;
    files: Array<MangaFile>;
    /**
     * This is used in the UI, it is not updated or sent to Backend
     */
    coverImage: string;
    coverImageLocked: boolean;
    pages: number;
    volumeId: number;
    pagesRead: number; // Attached for the given user when requesting from API
    isSpecial: boolean;
    title: string;
    created: string;
}
