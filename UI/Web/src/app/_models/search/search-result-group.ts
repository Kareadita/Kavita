import { Chapter } from "../chapter";
import { Library } from "../library";
import { MangaFile } from "../manga-file";
import { SearchResult } from "./search-result";
import { Tag } from "../tag";

export class SearchResultGroup {
    libraries: Array<Library> = [];
    series: Array<SearchResult> = [];
    collections: Array<Tag> = [];
    readingLists: Array<Tag> = [];
    persons: Array<Tag> = [];
    genres: Array<Tag> = [];
    tags: Array<Tag> = [];
    files: Array<MangaFile> = [];
    chapters: Array<Chapter> = [];

    reset() {
        this.libraries = [];
        this.series = [];
        this.collections = [];
        this.readingLists = [];
        this.persons = [];
        this.genres = [];
        this.tags = [];
        this.files = [];
        this.chapters = []; 
    }
}