export interface BookChapterItem {
    title: string;
    page: number;
    part: string;
    children: Array<BookChapterItem>;
}
