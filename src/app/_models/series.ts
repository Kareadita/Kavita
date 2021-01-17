import { Volume } from './volume';

export interface Series {
    id: number;
    name: string;
    originalName: string;
    sortName: string;
    summary: string;
    coverImage: string;
    volumes: Volume[];
    pages: number; // Total pages in series
    pagesRead: number; // Total pages the logged in user has read
}
