import { CollectionTag } from "./collection-tag";
import { Genre } from "./genre";
import { Person } from "./person";

export interface SeriesMetadata {
    publisher: string;
    summary: string;
    genres: Array<Genre>;
    tags: Array<CollectionTag>;
    writers: Array<Person>;
    artists: Array<Person>;
    publishers: Array<Person>;
    characters: Array<Person>;

    seriesId: number;
}