//TODO: Refactor this name to something better
export interface InProgressChapter {
    id: number;
    range: string;
    number: string;
    pages: number;
    volumeId: number;
    pagesRead: number;
    seriesId: number;
    seriesName: string;
    coverImage: string;
    libraryId: number;
    libraryName: string;
}
