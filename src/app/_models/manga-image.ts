export interface MangaImage {
    page: number;
    filename: string;
    fullPath: string;
    width: number;
    height: number;
    format: string;
    content: any;
    contentUrl?: string;
    needsSplitting: boolean;
    mangaFileName: string;
}
