export enum LibraryType {
    Manga = 0,
    Comic = 1,
    Book = 2,
}

export interface Library {
    id: number;
    name: string;
    lastScanned: string;
    type: LibraryType;
    folders: string[];
    coverImage?: string;
    // TODO: Should I move these into extended Library to reduce overhead unless editing? 
    folderWatching: boolean;
    includeInDashboard: boolean;
    includeInRecommended: boolean;

}