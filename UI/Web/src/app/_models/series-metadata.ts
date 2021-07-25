import { CollectionTag } from "./collection-tag";
import { Person } from "./person";

export interface SeriesMetadata {
    publisher: string;
    genres: Array<string>;
    tags: Array<CollectionTag>;
    persons: Array<Person>;
    seriesId: number;
}