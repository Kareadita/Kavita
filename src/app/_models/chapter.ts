import { MangaFile } from "./manga-file";

export interface Chatper {
    id: number;
    range: string;
    number: string;
    files: Array<MangaFile>;
    coverImage: string;
    pages: number;
    volumeId: number;
    pagesRead: number; // Attached for the given user when requesting from API
}
