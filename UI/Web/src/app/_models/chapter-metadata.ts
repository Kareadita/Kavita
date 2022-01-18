import { Person } from "./person";

export interface ChapterMetadata {
    id: number;
    chapterId: number;
    title: string;
    year: string;
    writers: Array<Person>;
    penciller: Array<Person>;
    inker: Array<Person>;
    colorist: Array<Person>;
    letterer: Array<Person>;
    coverArtist: Array<Person>;
    editor: Array<Person>;
    publishers: Array<Person>;
}