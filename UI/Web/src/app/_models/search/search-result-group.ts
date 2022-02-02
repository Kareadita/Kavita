import { SearchResult } from "../search-result";
import { Tag } from "../tag";

export class SearchResultGroup {
    series: Array<SearchResult> = [];
    collections: Array<Tag> = [];
    readingLists: Array<Tag> = [];
    persons: Array<Tag> = [];
    genres: Array<Tag> = [];
    tags: Array<Tag> = [];

    reset() {
        this.series = [];
        this.collections = [];
        this.readingLists = [];
        this.persons = [];
        this.genres = [];
        this.tags = [];
    }
}