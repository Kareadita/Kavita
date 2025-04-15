export interface ReadHistoryEvent {
    userId: number;
    userName: string;
    seriesName: string;
    seriesId: number;
    libraryId: number;
    readDate: string;
    readDateUtc: string;
    chapterId: number;
    chapterNumber: number;
}
