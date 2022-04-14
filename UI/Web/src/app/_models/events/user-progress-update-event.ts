export interface UserProgressUpdateEvent {
    userId: number;
    username: string;
    //entityId: number;
    //entityType: 'series' | 'collection' | 'chapter' | 'volume' | 'readingList';
    seriesId: number;
    volumeId: number;
    chapterId: number;
    pagesRead: number;
}