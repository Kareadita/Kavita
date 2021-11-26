import { CollectionTag } from "./collection-tag";
import { Person } from "./person";

export interface SeriesMetadata {
    publisher: string;
    summary: string;
    genres: Array<string>;
    tags: Array<CollectionTag>;
    writers: Array<Person>;
    artists: Array<Person>;
    publishers: Array<Person>;
    characters: Array<Person>;

    seriesId: number;
}