import { Library } from "../library";
import { SearchResult } from "../search-result";
import { Tag } from "../tag";

export class SearchResultGroup {
    libraries: Array<Library> = [];
    series: Array<SearchResult> = [];
    collections: Array<Tag> = [];
    readingLists: Array<Tag> = [];
    persons: Array<Tag> = [];
    genres: Array<Tag> = [];
    tags: Array<Tag> = [];

    reset() {
        this.libraries = [];
        this.series = [];
        this.collections = [];
        this.readingLists = [];
        this.persons = [];
        this.genres = [];
        this.tags = [];
    }
}