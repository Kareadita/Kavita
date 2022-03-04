import { Genre } from './genre';
import { MangaFile } from './manga-file';
import { AgeRating } from './metadata/age-rating';
import { PublicationStatus } from './metadata/publication-status';
import { Person } from './person';
import { Tag } from './tag';

export interface Chapter {
    id: number;
    range: string;
    number: string;
    files: Array<MangaFile>;
    /**
     * This is used in the UI, it is not updated or sent to Backend
     */
    coverImage: string;
    coverImageLocked: boolean;
    pages: number;
    volumeId: number;
    pagesRead: number; // Attached for the given user when requesting from API
    isSpecial: boolean;
    title: string;
    created: string;

    titleName: string;
    /**
     * This is only Year and Month, Day is not supported from underlying sources
     */
    // releaseDate: string;
    // writers: Array<Person>;
    // pencillers: Array<Person>;
    // inkers: Array<Person>;
    // colorists: Array<Person>;
    // letterers: Array<Person>;
    // coverArtists: Array<Person>;
    // editors: Array<Person>;
    // publishers: Array<Person>;
    // translators: Array<Person>;
    // characters: Array<Person>;
    // tags: Array<Tag>;
    // genres: Array<Genre>;
    
    // ageRating: AgeRating;
    // releaseYear: number;
    // language: string;
    // publicationStatus: PublicationStatus;
}
