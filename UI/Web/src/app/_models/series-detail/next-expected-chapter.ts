export interface NextExpectedChapter {
    volumeNumber: number;
    chapterNumber: number;
    expectedDate: string | null;
    title: string;
    /**
     * Not real, used for some type stuff with app-card
     */
    id: number;
}
