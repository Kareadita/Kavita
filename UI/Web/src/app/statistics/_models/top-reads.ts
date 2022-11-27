export interface TopRead {
    seriesName: string;
    seriesId: number;
    libraryId: number;
    usersRead: number;
}

export interface TopReads {
    manga: Array<TopRead>;
    comics: Array<TopRead>;
    books: Array<TopRead>;
}