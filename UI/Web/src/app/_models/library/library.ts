import {FileTypeGroup} from "./file-type-group.enum";

export enum LibraryType {
    Manga = 0,
    Comic = 1,
    Book = 2,
    Images = 3,
    LightNovel = 4,
    /**
     * Comic (Legacy)
     */
    ComicVine = 5
}

export const allLibraryTypes = [LibraryType.Manga, LibraryType.ComicVine, LibraryType.Comic, LibraryType.Book, LibraryType.LightNovel, LibraryType.Images];
export const allKavitaPlusMetadataApplicableTypes = [LibraryType.Manga, LibraryType.LightNovel, LibraryType.ComicVine, LibraryType.Comic];
export const allKavitaPlusScrobbleEligibleTypes = [LibraryType.Manga, LibraryType.LightNovel];

export interface Library {
    id: number;
    name: string;
    lastScanned: string;
    type: LibraryType;
    folders: string[];
    coverImage?: string | null;
    folderWatching: boolean;
    includeInDashboard: boolean;
    includeInRecommended: boolean;
    includeInSearch: boolean;
    manageCollections: boolean;
    manageReadingLists: boolean;
    allowScrobbling: boolean;
    allowMetadataMatching: boolean;
    enableMetadata: boolean;
    collapseSeriesRelationships: boolean;
    libraryFileTypes: Array<FileTypeGroup>;
    excludePatterns: Array<string>;
}
