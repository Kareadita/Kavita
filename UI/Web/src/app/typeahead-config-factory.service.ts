import {DestroyRef, inject, Injectable} from '@angular/core';
import {TypeaheadConfig} from "./typeahead/_models/typeahead-config";
import {Person, PersonRole} from "./_models/metadata/person";
import {map, shareReplay} from "rxjs/operators";
import {UtilityService} from "./shared/_services/utility.service";
import {MetadataService} from "./_services/metadata.service";
import {Chapter} from "./_models/chapter";
import {Library} from "./_models/library/library";
import {of} from "rxjs";
import {AccountService} from "./_services/account.service";
import {Language} from "./_models/metadata/language";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SearchResult} from "./_models/search/search-result";
import {SearchService} from "./_services/search.service";
import {Tag} from "./_models/tag";
import {Genre} from "./_models/metadata/genre";
import {ReadingListTag} from "./_models/reading-list/reading-list-tag";

/**
 * Partial configuration for overrides. All properties optional.
 *
 * Note: setting addIfNonExisting: true is only valid when the factory method you
 * called also supplies compareFnForAdd and addTransformFn — the typeahead asserts
 * both non-null at runtime. Prefer the addIfNonExisting param where one exists.
 */
export type TypeaheadConfigurationOverrides<T> = Partial<TypeaheadConfig<T>>;

export interface TypeaheadFactoryParameters<T> {
  /**
   * Id of the input element, for linking label elements (accessibility). Required — no sensible default.
   */
  id: string;
  /**
   * Data to preload the typeahead with. Also settable via overrides.savedData (overrides win)
   * or by assigning to the returned instance once an async fetch resolves.
   */
  savedData?: T[];
  overrides?: TypeaheadConfigurationOverrides<T>;
}

export interface TypeaheadFactoryPersonParameters extends TypeaheadFactoryParameters<Person> {
  role?: PersonRole;
  addIfNonExisting?: boolean;
}

export interface TypeaheadFactoryChapterParameters extends TypeaheadFactoryParameters<Chapter> {
  seriesId: number;
}

export interface TypeaheadFactoryLibraryParameters extends TypeaheadFactoryParameters<Library> {
  libraries: Library[]
}

export interface TypeaheadFactoryLanguageParameters extends TypeaheadFactoryParameters<Language> {
  currentSelectedLanguage?: string | Array<string> | undefined;
}

export interface TypeaheadFactorySearchResultParameters extends TypeaheadFactoryParameters<SearchResult> {
  excludeSeriesId?: number;
}

export type TypeaheadFactoryTagParameters = TypeaheadFactoryParameters<Tag>;

export type TypeaheadFactoryReadingListTagParameters = TypeaheadFactoryParameters<ReadingListTag>;

export type TypeaheadFactoryGenreParameters = TypeaheadFactoryParameters<Genre>;

@Injectable({providedIn: 'root'})
export class TypeaheadConfigFactoryService {

  private readonly utilityService = inject(UtilityService);
  private readonly metadataService = inject(MetadataService);
  private readonly accountService = inject(AccountService);
  private readonly searchService = inject(SearchService);
  private readonly destroyRef = inject(DestroyRef);


  private readonly allLanguages$ = this.metadataService.getAllValidLanguages()
    .pipe(shareReplay({bufferSize: 1, refCount: false}), takeUntilDestroyed(this.destroyRef));

  constructor() {
    this.allLanguages$.subscribe(); // pre-cache the language for the session
  }

  forLibraries(params: TypeaheadFactoryLibraryParameters) {
    const {libraries, savedData, overrides} = params;

    const selectedLibs = this.accountService.userPreferences()!.socialPreferences.socialLibraries;

    const settings = new TypeaheadConfig<Library>();
    settings.multiple = true;
    settings.unique = true;
    settings.minCharacters = 0;
    settings.addIfNonExisting = false;
    settings.savedData = libraries.filter(l => selectedLibs.includes(l.id));
    settings.compareFn = (libs, filter) => libs.filter(l => l.name.toLowerCase().includes(filter.toLowerCase()));
    settings.compareFnForAdd = (options: Library[], filter: string) => {
      return options.filter(l => this.utilityService.filterMatches(l.name, filter));
    }
    settings.trackByIdentityFn = (idx, l) => `${l.id}`;
    settings.titleFn = (l) => l.name;
    settings.fetchFn = (filter) => of(settings.compareFn(libraries, filter));
    settings.selectionCompareFn = (a: Library, b: Library) => {
      return a.id === b.id;
    }

    if (savedData !== undefined) settings.savedData = savedData;

    return this.applyOverrides(settings, overrides);
  }

  forPerson(params: TypeaheadFactoryPersonParameters) {
    const {id, role, addIfNonExisting = true, savedData, overrides} = params;

    const settings = new TypeaheadConfig<Person>();
    settings.id = id;
    settings.minCharacters = 0;
    settings.multiple = true;
    settings.showLocked = true;
    settings.unique = true;
    settings.addIfNonExisting = addIfNonExisting;

    settings.compareFn = (options, filter) => options.filter(m => this.utilityService.filter(m.name, filter));
    settings.compareFnForAdd = (options, filter) => options.filter(m => this.utilityService.filterMatches(m.name,
      filter));
    settings.selectionCompareFn = (a, b) => a.name === b.name;

    settings.fetchFn = (filter) => this.metadataService.getAllPeople()
      .pipe(map(items => settings.compareFn(items, filter)));

    settings.addTransformFn = (title) => {
      const newPerson = {id: 0, name: title, aliases: [], description: '', coverImage: '',
        coverImageLocked: false, primaryColor: '', secondaryColor: ''};
      return role ? {role, ...newPerson} : newPerson;
    };

    settings.trackByIdentityFn = (_, value) => value.name + value.id;
    settings.titleFn = (value) => value.name;

    if (savedData !== undefined) settings.savedData = savedData;

    return this.applyOverrides(settings, overrides);

  }


  forLanguage(params: TypeaheadFactoryLanguageParameters) {
    const {id, currentSelectedLanguage, savedData, overrides} = params;
    const settings = new TypeaheadConfig<Language>();


    settings.minCharacters = 0;
    settings.multiple = false;
    settings.id = id;
    settings.unique = true;
    settings.showLocked = true;
    settings.addIfNonExisting = false;
    settings.compareFn = (options: Language[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    }
    settings.compareFnForAdd = (options: Language[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    }
    settings.fetchFn = (filter: string) => this.allLanguages$
      .pipe(map(items => settings.compareFn(items, filter)));

    settings.selectionCompareFn = (a: Language, b: Language) => {
      return a.isoCode === b.isoCode;
    }

    settings.trackByIdentityFn = (_, value) => value.isoCode;
    settings.titleFn = (value) => value.title;

    // Language works differently, savedData isn't passed but currentSelectedLanguage is
    if (currentSelectedLanguage) {
      // We pre-call this so it's already cached in memory
      this.allLanguages$.subscribe(languages => {
        settings.savedData = languages.filter(l => l.isoCode === currentSelectedLanguage);
      });
    } else if (savedData) {
      settings.savedData = savedData;
    }

    return this.applyOverrides(settings, overrides);
  }

  forSearchResult(params: TypeaheadFactorySearchResultParameters) {
    const {id, savedData, excludeSeriesId, overrides} = params;

    const settings = new TypeaheadConfig<SearchResult>();
    settings.minCharacters = 2;
    settings.multiple = false;
    settings.id = id;
    settings.unique = true;
    settings.addIfNonExisting = false;
    settings.fetchFn = (searchFilter: string) => this.searchService.search(searchFilter).pipe(
      map(group => group.series),
      map(items => settings.compareFn(items, searchFilter)),
      map(series => series.filter(s => !excludeSeriesId || s.seriesId !== excludeSeriesId)),
    );
    settings.trackByIdentityFn = (idx, item) => item.seriesId + '';
    settings.titleFn = (item) => item.name;
    settings.compareFn = (options: SearchResult[], filter: string) => {
      return options.filter(m => {
        return this.utilityService.filter(m.name, filter) || this.utilityService.filter(m.localizedName, filter);
      });
    };
    settings.selectionCompareFn = (a: SearchResult, b: SearchResult) => {
      return a.seriesId === b.seriesId;
    };
    settings.dropdownPosition = 'body';
    settings.overlayMinWidth = 400;

    if (savedData !== undefined) settings.savedData = savedData;

    return this.applyOverrides(settings, overrides);
  }

  forTag(params: TypeaheadFactoryTagParameters) {
    const {id, savedData, overrides} = params;

    const settings = new TypeaheadConfig<Tag>();
    settings.minCharacters = 0;
    settings.multiple = true;
    settings.id = id;
    settings.unique = true;
    settings.showLocked = true;
    settings.addIfNonExisting = true;

    settings.trackByIdentityFn = (_idx, item) => item.title + item.id;
    settings.titleFn = (item) => item.title;
    settings.selectionCompareFn = (a: Tag, b: Tag) => {
      return a.title.toLowerCase() == b.title.toLowerCase();
    };
    settings.compareFn = (options: Tag[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    };
    settings.compareFnForAdd = (options: Tag[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    };
    settings.fetchFn = (filter: string) => {
      return this.metadataService.getAllTags().pipe(map(items => settings.compareFn(items, filter)));
    };
    settings.addTransformFn = ((title: string) => {
      return {id: 0, title: title };
    });

    if (savedData !== undefined) settings.savedData = savedData;

    return this.applyOverrides(settings, overrides);
  }


  forReadingListTag(params: TypeaheadFactoryReadingListTagParameters) {
    const {id, savedData, overrides} = params;

    const settings = new TypeaheadConfig<ReadingListTag>();
    settings.minCharacters = 0;
    settings.multiple = true;
    settings.id = id;
    settings.unique = true;
    settings.showLocked = true;
    settings.addIfNonExisting = true;

    settings.trackByIdentityFn = (_idx, item) => item.title + item.id;
    settings.titleFn = (item) => item.title;
    settings.selectionCompareFn = (a: Tag, b: Tag) => {
      return a.title.toLowerCase() == b.title.toLowerCase();
    };
    settings.compareFn = (options: ReadingListTag[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    };
    settings.compareFnForAdd = (options: ReadingListTag[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    };
    settings.fetchFn = (filter: string) => {
      const tags$ = this.metadataService.getAllReadingListTags();
      return tags$.pipe(map(items => settings.compareFn(items, filter)));
    };
    settings.addTransformFn = ((title: string) => {
      return {id: 0, title: title, normalizedTitle: title.toLowerCase() };
    });

    if (savedData !== undefined) settings.savedData = savedData;

    return this.applyOverrides(settings, overrides);
  }

  forGenre(params: TypeaheadFactoryGenreParameters) {
    const {id, savedData, overrides} = params;

    const settings = new TypeaheadConfig<Genre>();
    settings.minCharacters = 0;
    settings.multiple = true;
    settings.id = id;
    settings.unique = true;
    settings.showLocked = true;
    settings.addIfNonExisting = true;

    settings.trackByIdentityFn = (_idx, item) => item.title + item.id;
    settings.titleFn = (item) => item.title;
    settings.selectionCompareFn = (a: Genre, b: Genre) => {
      return a.title.toLowerCase() == b.title.toLowerCase();
    };
    settings.compareFn = (options: Genre[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    };
    settings.compareFnForAdd = (options: Genre[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    };
    settings.fetchFn = (filter: string) => this.metadataService.getAllGenres()
      .pipe(map(items => settings.compareFn(items, filter)));
    settings.addTransformFn = ((title: string) => {
      return {id: 0, title: title };
    });

    if (savedData !== undefined) settings.savedData = savedData;

    return this.applyOverrides(settings, overrides);
  }

  forChapter(params: TypeaheadFactoryChapterParameters) {
    const {id, savedData, seriesId, overrides} = params;

    const settings = new TypeaheadConfig<Chapter>();
    settings.minCharacters = 0;
    settings.multiple = false;
    settings.id = id;
    settings.unique = true;
    settings.addIfNonExisting = false;
    settings.fetchFn = (searchFilter: string) => this.searchService.getChaptersBySeries(seriesId).pipe(
      map(chapters => {
        if (!searchFilter) return chapters;
        const lower = searchFilter.toLowerCase().trim();
        return chapters.filter(c =>
          c.title?.toLowerCase().includes(lower) ||
          c.range?.toLowerCase().includes(lower) ||
          c.titleName?.toLowerCase().includes(lower)
        );
      })
    );
    settings.trackByIdentityFn = (_idx, item) => item.id + '';
    settings.titleFn = (item) => item.title;
    settings.compareFn = (options: Chapter[], filter: string) => {
      if (!filter) return options;
      const lower = filter.toLowerCase().trim();
      return options.filter(c =>
        c.title?.toLowerCase().includes(lower) ||
        c.range?.toLowerCase().includes(lower) ||
        c.titleName?.toLowerCase().includes(lower)
      );
    };
    settings.selectionCompareFn = (a: Chapter, b: Chapter) => {
      return a.id === b.id;
    };

    if (savedData !== undefined) settings.savedData = savedData;

    return this.applyOverrides(settings, overrides);
  }

  /**
   * Applies overrides onto the settings instance. Mutates rather than spreading so that
   * closures created in the factory (fetchFn reading settings.compareFn) observe the
   * overridden values. Undefined values are skipped so they can't clobber class defaults.
   */
  private applyOverrides<T>(
    settings: TypeaheadConfig<T>,
    overrides?: TypeaheadConfigurationOverrides<T>
  ): TypeaheadConfig<T> {
    if (!overrides) return settings;

    const defined = Object.fromEntries(
      Object.entries(overrides).filter(([, value]) => value !== undefined)
    );
    Object.assign(settings, defined);

    return settings;
  }
}
