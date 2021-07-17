export interface SearchResult {
    seriesId: number;
    libraryId: number;
    libraryName: string;
    name: string;
    originalName: string;
    sortName: string;
    coverImage: string; // byte64 encoded
}
