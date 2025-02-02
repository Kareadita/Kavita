import {FileTypeGroup} from "./file-type-group.enum";
import {IHasCover} from "../common/i-has-cover";

export enum LibraryType {
    Manga = 0,
    Comic = 1,
    Book = 2,
    Images = 3,
    LightNovel = 4,
    ComicVine = 5
}

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
    collapseSeriesRelationships: boolean;
    libraryFileTypes: Array<FileTypeGroup>;
    excludePatterns: Array<string>;
}
