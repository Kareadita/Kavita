export enum LibraryType {
    Manga = 0,
    Comic = 1,
    Book = 2,
    MangaImages = 3,
    ComicImages = 4
}

export interface Library {
    id: number;
    name: string;
    coverImage: string;
    type: LibraryType;
    folders: string[];
}