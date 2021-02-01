import { Chatper } from './chapter';

export interface Volume {
    id: number;
    number: number;
    name: string;
    coverImage: string;
    created: string;
    lastModified: string;
    pages: number;
    pagesRead: number;
    chapters?: Array<Chatper>;
    isSpecial: boolean; // For side stories, etc.
}
