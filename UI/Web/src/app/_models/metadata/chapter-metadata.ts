import { Genre } from "./genre";
import { AgeRating } from "./age-rating";
import { PublicationStatus } from "./publication-status";
import { Person } from "./person";
import { Tag } from "../tag";

export interface ChapterMetadata {
    id: number;
    chapterId: number;
    title: string;
    year: string;

    ageRating: AgeRating;
    releaseDate: string;
    language: string;
    publicationStatus: PublicationStatus;
    summary: string;
    count: number;
    totalCount: number;
    wordCount: number;



    genres: Array<Genre>;
    tags: Array<Tag>;
    writers: Array<Person>;
    coverArtists: Array<Person>;
    publishers: Array<Person>;
    characters: Array<Person>;
    pencillers: Array<Person>;
    inkers: Array<Person>;
    imprints: Array<Person>;
    colorists: Array<Person>;
    letterers: Array<Person>;
    editors: Array<Person>;
    translators: Array<Person>;



}
