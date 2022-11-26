import { MangaFormat } from "src/app/_models/manga-format";

export interface FileExtension {
    extension: string;
    format: MangaFormat;
    totalSize: number;
    totalFiles: number;
}

export interface FileExtensionBreakdown {
    totalFileSize: number;
    fileBreakdown: Array<FileExtension>;
}