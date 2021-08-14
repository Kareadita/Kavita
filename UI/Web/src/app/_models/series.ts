import { MangaFormat } from './manga-format';
import { Volume } from './volume';

export interface Series {
    id: number;
    name: string;
    originalName: string; // This is not shown to user
    localizedName: string;
    sortName: string;
    summary: string;
    coverImage: string; // This is not passed from backend any longer. TODO: Remove this field
    coverImageLocked: boolean;
    volumes: Volume[];
    pages: number; // Total pages in series
    pagesRead: number; // Total pages the logged in user has read
    userRating: number; // User rating
    userReview: string; // User review
    libraryId: number;
    created: string; // DateTime when entity was created
    format: MangaFormat;
}
