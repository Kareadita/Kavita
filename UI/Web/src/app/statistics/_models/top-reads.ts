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

export interface TopUserRead {
    userId: number;
    username: string;
    mangaTime: number;
    comicsTime: number;
    booksTime: number;
}