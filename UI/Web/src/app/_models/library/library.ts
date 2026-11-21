import {FileTypeGroup} from "./file-type-group.enum";
import {MetadataProvider} from "../kavitaplus/metadata-provider.enum";
import {FolderWatchMode} from "./folder-watch-mode";
import {IHasCover} from "../common/i-has-cover";

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

export interface LiteLibrary {
  id: number;
  name: string;
  type: LibraryType;
}

export interface Library extends LiteLibrary, IHasCover {
    id: number;
    name: string;
    type: LibraryType;
    lastScanned: string;
    folders: string[];
    folderWatchMode: FolderWatchMode;
    includeInDashboard: boolean;
    includeInRecommended: boolean;
    includeInSearch: boolean;
    manageCollections: boolean;
    manageReadingLists: boolean;
    allowScrobbling: boolean;
    allowMetadataMatching: boolean;
    enableMetadata: boolean;
    removePrefixForSortName: boolean;
    collapseSeriesRelationships: boolean;
    inheritWebLinksFromFirstChapter: boolean;
    defaultLanguage: string;
    metadataProvider: MetadataProvider;
    libraryFileTypes: Array<FileTypeGroup>;
    excludePatterns: Array<string>;
}
