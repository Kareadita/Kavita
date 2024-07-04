import { Chapter } from "../chapter";
import { Library } from "../library/library";
import { MangaFile } from "../manga-file";
import { SearchResult } from "./search-result";
import { Tag } from "../tag";
import {BookmarkSearchResult} from "./bookmark-search-result";
import {Genre} from "../metadata/genre";
import {ReadingList} from "../reading-list";
import {UserCollection} from "../collection-tag";

export class SearchResultGroup {
    libraries: Array<Library> = [];
    series: Array<SearchResult> = [];
    collections: Array<UserCollection> = [];
    readingLists: Array<ReadingList> = [];
    persons: Array<string> = [];
    genres: Array<Genre> = [];
    tags: Array<Tag> = [];
    files: Array<MangaFile> = [];
    chapters: Array<Chapter> = [];
    bookmarks: Array<BookmarkSearchResult> = [];

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
        this.bookmarks = [];
    }
}
