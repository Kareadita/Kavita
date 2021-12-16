import { Component, ContentChild, EventEmitter, Input, OnDestroy, OnInit, Output, TemplateRef } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Observable, of, ReplaySubject, Subject } from 'rxjs';
import { map, takeUntil } from 'rxjs/operators';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { TypeaheadSettings } from 'src/app/typeahead/typeahead-settings';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { Genre } from 'src/app/_models/genre';
import { Library } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Pagination } from 'src/app/_models/pagination';
import { Person, PersonRole } from 'src/app/_models/person';
import { FilterItem, mangaFormatFilters, SeriesFilter, SortField } from 'src/app/_models/series-filter';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { LibraryService } from 'src/app/_services/library.service';
import { MetadataService } from 'src/app/_services/metadata.service';
import { SeriesService } from 'src/app/_services/series.service';

const FILTER_PAG_REGEX = /[^0-9]/g;

const ANIMATION_SPEED = 300;

export class FilterSettings {
  libraryDisabled = false;
  formatDisabled = false;
  collectionDisabled = false;
  genresDisabled = false;
  peopleDisabled = false;
  readProgressDisabled = false;
  ratingDisabled = false;
  presetLibraryId = 0;
  presetCollectionId = 0;
  sortDisabled = false;
}

@Component({
  selector: 'app-card-detail-layout',
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss']
})
export class CardDetailLayoutComponent implements OnInit, OnDestroy {

  @Input() header: string = '';
  @Input() isLoading: boolean = false; 
  @Input() items: any[] = [];
  @Input() pagination!: Pagination;
  /**
   * Any actions to exist on the header for the parent collection (library, collection)
   */
  @Input() actions: ActionItem<any>[] = [];
  @Input() trackByIdentity!: (index: number, item: any) => string;
  @Input() filterSettings!: FilterSettings;
  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() pageChange: EventEmitter<Pagination> = new EventEmitter();
  @Output() applyFilter: EventEmitter<SeriesFilter> = new EventEmitter();
  
  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;
  

  formatSettings: TypeaheadSettings<FilterItem<MangaFormat>> = new TypeaheadSettings();
  librarySettings: TypeaheadSettings<FilterItem<Library>> = new TypeaheadSettings();
  genreSettings: TypeaheadSettings<FilterItem<Genre>> = new TypeaheadSettings();
  collectionSettings: TypeaheadSettings<FilterItem<CollectionTag>> = new TypeaheadSettings();
  peopleSettings: {[PersonRole: string]: TypeaheadSettings<FilterItem<Person>>} = {};
  resetTypeaheads: Subject<boolean> = new ReplaySubject(1);

  /**
   * Controls the visiblity of extended controls that sit below the main header.
   */
  filteringCollapsed: boolean = true;

  filter!: SeriesFilter;
  libraries: Array<FilterItem<Library>> = [];
  genres: Array<FilterItem<Genre>> = [];
  persons: Array<FilterItem<Person>> = [];
  collectionTags: Array<FilterItem<CollectionTag>> = [];

  readProgressGroup!: FormGroup;
  sortGroup!: FormGroup;
  isAscendingSort: boolean = true;

  updateApplied: number = 0;

  private onDestory: Subject<void> = new Subject();

  get PersonRole(): typeof PersonRole {
    return PersonRole;
  }

  get SortField(): typeof SortField {
    return SortField;
  }

  constructor(private libraryService: LibraryService, private metadataService: MetadataService, private seriesService: SeriesService,
    private utilityService: UtilityService, private collectionTagService: CollectionTagService) {
    this.filter = this.seriesService.createSeriesFilter();
    this.readProgressGroup = new FormGroup({
      read: new FormControl(this.filter.readStatus.read, []),
      notRead: new FormControl(this.filter.readStatus.notRead, []),
      inProgress: new FormControl(this.filter.readStatus.inProgress, []),
    });

    this.sortGroup = new FormGroup({
      sortField: new FormControl(this.filter.sortOptions?.sortField || SortField.SortName, []),
    });

    this.readProgressGroup.valueChanges.pipe(takeUntil(this.onDestory)).subscribe(changes => {
      this.filter.readStatus.read = this.readProgressGroup.get('read')?.value;
      this.filter.readStatus.inProgress = this.readProgressGroup.get('inProgress')?.value;
      this.filter.readStatus.notRead = this.readProgressGroup.get('notRead')?.value;

      let sum = 0;
      sum += (this.filter.readStatus.read ? 1 : 0);
      sum += (this.filter.readStatus.inProgress ? 1 : 0);
      sum += (this.filter.readStatus.notRead ? 1 : 0);

      if (sum === 1) {
        if (this.filter.readStatus.read) this.readProgressGroup.get('read')?.disable({ emitEvent: false });
        if (this.filter.readStatus.notRead) this.readProgressGroup.get('notRead')?.disable({ emitEvent: false });
        if (this.filter.readStatus.inProgress) this.readProgressGroup.get('inProgress')?.disable({ emitEvent: false });
      } else {
        this.readProgressGroup.get('read')?.enable({ emitEvent: false });
        this.readProgressGroup.get('notRead')?.enable({ emitEvent: false });
        this.readProgressGroup.get('inProgress')?.enable({ emitEvent: false });
      }
    });

    this.sortGroup.valueChanges.pipe(takeUntil(this.onDestory)).subscribe(changes => {
      if (this.filter.sortOptions == null) {
        this.filter.sortOptions = {
          isAscending: this.isAscendingSort,
          sortField: parseInt(this.sortGroup.get('sortField')?.value, 10)
        };
      }
      this.filter.sortOptions.sortField = parseInt(this.sortGroup.get('sortField')?.value, 10);
    });
  }

  ngOnInit(): void {
    this.trackByIdentity = (index: number, item: any) => `${this.header}_${this.pagination?.currentPage}_${this.updateApplied}`;
    this.setupFormatTypeahead();

    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettings();
    }
    
    let apiCall;
    if (this.filter.libraries.length > 0) {
      apiCall = this.metadataService.getGenresForLibraries(this.filter.libraries);
    } else {
      apiCall = this.metadataService.getAllGenres();
    }

    apiCall.subscribe(genres => {
      this.genres = genres.map(genre => {
        return {
          title: genre.title,
          value: genre,
          selected: false,
        }
      });
      this.setupGenreTypeahead();

    });

    this.libraryService.getLibrariesForMember().subscribe(libs => {
      this.libraries = libs.map(lib => {
        return {
          title: lib.name,
          value: lib,
          selected: true,
        }
      });
      this.setupLibraryTypeahead();
    });

    this.metadataService.getAllPeople().subscribe(res => {
      this.persons = res.map(lib => {
        return {
          title: lib.name,
          value: lib,
          selected: true,
        }
      });
      this.setupPersonTypeahead();
    });

    this.collectionTagService.allTags().subscribe(tags => {
      this.collectionTags = tags.map(lib => {
        return {
          title: lib.title,
          value: lib,
          selected: false,
        }
      });
      this.setupCollectionTagTypeahead();
    });
  }

  ngOnDestroy() {
    this.onDestory.next();
    this.onDestory.complete();
  }


  setupFormatTypeahead() {
    this.formatSettings.minCharacters = 0;
    this.formatSettings.multiple = true;
    this.formatSettings.id = 'format';
    this.formatSettings.unique = true;
    this.formatSettings.addIfNonExisting = false;
    this.formatSettings.fetchFn = (filter: string) => of(mangaFormatFilters);
    this.formatSettings.compareFn = (options: FilterItem<MangaFormat>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }
  }

  setupLibraryTypeahead() {
    this.librarySettings.minCharacters = 0;
    this.librarySettings.multiple = true;
    this.librarySettings.id = 'libraries';
    this.librarySettings.unique = true;
    this.librarySettings.addIfNonExisting = false;
    this.librarySettings.fetchFn = (filter: string) => {
      return of (this.libraries)
    };
    this.librarySettings.compareFn = (options: FilterItem<Library>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }

    if (this.filterSettings.presetLibraryId > 0) {
      this.librarySettings.savedData = this.libraries.filter(item => item.value.id === this.filterSettings.presetLibraryId);
      this.filter.libraries = this.librarySettings.savedData.map(item => item.value.id);
      this.resetTypeaheads.next(true); // For some reason library just doesn't update properly with savedData
    }
  }

  setupGenreTypeahead() {
    this.genreSettings.minCharacters = 0;
    this.genreSettings.multiple = true;
    this.genreSettings.id = 'genres';
    this.genreSettings.unique = true;
    this.genreSettings.addIfNonExisting = false;
    this.genreSettings.fetchFn = (filter: string) => {
      let apiCall;
      if (this.filter.libraries.length > 0) {
        apiCall = this.metadataService.getGenresForLibraries(this.filter.libraries);
      } else {
        apiCall = this.metadataService.getAllGenres();
      }
      return apiCall.pipe(map(genres => {
        return genres.map(genre => {
          return {
            title: genre.title,
            value: genre,
            selected: false,
          }
        })
      }));
     // return of (this.genres)
    };
    this.genreSettings.compareFn = (options: FilterItem<Genre>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }
  }

  setupCollectionTagTypeahead() {
    this.collectionSettings.minCharacters = 0;
    this.collectionSettings.multiple = true;
    this.collectionSettings.id = 'collections';
    this.collectionSettings.unique = true;
    this.collectionSettings.addIfNonExisting = false;
    this.collectionSettings.fetchFn = (filter: string) => {
      return of (this.collectionTags)
    };
    this.collectionSettings.compareFn = (options: FilterItem<CollectionTag>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }
    if (this.filterSettings.presetCollectionId > 0) {
      this.collectionSettings.savedData = this.collectionTags.filter(item => item.value.id === this.filterSettings.presetCollectionId);
      this.filter.collectionTags = this.collectionSettings.savedData.map(item => item.value.id);
      this.resetTypeaheads.next(true);
    }
  }

  setupPersonTypeahead() {
    this.peopleSettings = {};

    var personSettings = this.createBlankPersonSettings('writers');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.Writer, filter);
    };
    this.peopleSettings[PersonRole.Writer] = personSettings;

    personSettings = this.createBlankPersonSettings('character');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.Character, filter);
    };
    this.peopleSettings[PersonRole.Character] = personSettings;

    personSettings = this.createBlankPersonSettings('colorist');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.Colorist, filter);
    };
    this.peopleSettings[PersonRole.Colorist] = personSettings;

    personSettings = this.createBlankPersonSettings('cover-artist');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.CoverArtist, filter);
    };
    this.peopleSettings[PersonRole.CoverArtist] = personSettings;

    personSettings = this.createBlankPersonSettings('editor');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.Editor, filter);
    };
    this.peopleSettings[PersonRole.Editor] = personSettings;

    personSettings = this.createBlankPersonSettings('inker');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.Inker, filter);
    };
    this.peopleSettings[PersonRole.Inker] = personSettings;

    personSettings = this.createBlankPersonSettings('letterer');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.Letterer, filter);
    };
    this.peopleSettings[PersonRole.Letterer] = personSettings;

    personSettings = this.createBlankPersonSettings('penciller');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.Penciller, filter);
    };
    this.peopleSettings[PersonRole.Penciller] = personSettings;

    personSettings = this.createBlankPersonSettings('publisher');
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(PersonRole.Publisher, filter);
    };
    this.peopleSettings[PersonRole.Publisher] = personSettings;
  }

  fetchPeople(role: PersonRole, filter: string): Observable<FilterItem<Person>[]> {
    let apiCall;
    if (this.filter.libraries.length > 0) {
      apiCall = this.metadataService.getPeopleForLibraries(this.filter.libraries);
    } else {
      apiCall = this.metadataService.getAllPeople();
    }
    return apiCall.pipe(map(people => {
      return people.filter(p => p.role == role && this.utilityService.filter(p.name, filter)).map((p: Person) => {
        return {
          title: p.name,
          value: p,
          selected: false,
        }
      });
    }));
  }

  createBlankPersonSettings(id: string) {
    var personSettings = new TypeaheadSettings<FilterItem<Person>>();
    personSettings.minCharacters = 0;
    personSettings.multiple = true;
    personSettings.unique = true;
    personSettings.addIfNonExisting = false;
    personSettings.id = id;
    personSettings.compareFn = (options: FilterItem<Person>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }
    return personSettings;
  }


  onPageChange(page: number) {
    this.pageChange.emit(this.pagination);
  }

  selectPageStr(page: string) {
    this.pagination.currentPage = parseInt(page, 10) || 1;
    this.onPageChange(this.pagination.currentPage);
  }

  formatInput(input: HTMLInputElement) {
    input.value = input.value.replace(FILTER_PAG_REGEX, '');
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, undefined);
    }
  }


  updateFormatFilters(formats: FilterItem<MangaFormat>[]) {
    this.filter.formats = formats.map(item => item.value) || [];
  }

  updateLibraryFilters(libraries: FilterItem<Library>[]) {
    this.filter.libraries = libraries.map(item => item.value.id) || [];
  }

  updateGenreFilters(genres: FilterItem<Genre>[]) {
    this.filter.genres = genres.map(item => item.value.id) || [];
  }

  updatePersonFilters(persons: FilterItem<Person>[], role: PersonRole) {
    switch (role) {
      case PersonRole.CoverArtist:
        this.filter.coverArtist = persons.map(p => p.value.id);
        break;
      case PersonRole.Character:
        this.filter.character = persons.map(p => p.value.id);
        break;
      case PersonRole.Colorist:
        this.filter.colorist = persons.map(p => p.value.id);
        break;
      // case PersonRole.Artist:
      //   this.filter.artist = persons.map(p => p.value.id);
      //   break;
      case PersonRole.Editor:
        this.filter.editor = persons.map(p => p.value.id);
        break;
      case PersonRole.Inker:
        this.filter.inker = persons.map(p => p.value.id);
        break;
      case PersonRole.Letterer:
        this.filter.letterer = persons.map(p => p.value.id);
        break;
      case PersonRole.Penciller:
        this.filter.penciller = persons.map(p => p.value.id);
        break;
      case PersonRole.Publisher:
        this.filter.publisher = persons.map(p => p.value.id);
        break;
      case PersonRole.Writer:
        this.filter.writers = persons.map(p => p.value.id);
        break;

    }
  }

  updateCollectionFilters(tags: FilterItem<CollectionTag>[]) {
    this.filter.collectionTags = tags.map(item => item.value.id) || [];
  }

  updateRating(rating: any) {
    this.filter.rating = rating;
  }

  updateReadStatus(status: string) {
    console.log('readstatus: ', this.filter.readStatus);
    if (status === 'read') {
      this.filter.readStatus.read = !this.filter.readStatus.read;
    } else if (status === 'inProgress') {
      this.filter.readStatus.inProgress = !this.filter.readStatus.inProgress;
    } else if (status === 'notRead') {
      this.filter.readStatus.notRead = !this.filter.readStatus.notRead;
    }
  }

  updateSortOrder() {
    this.isAscendingSort = !this.isAscendingSort;
    if (this.filter.sortOptions !== null) {
      this.filter.sortOptions.isAscending = this.isAscendingSort;
    }
  }

  getPersonsSettings(role: PersonRole) {
    return this.peopleSettings[role];
  }

  clear() {
    this.filter = this.seriesService.createSeriesFilter();
    this.readProgressGroup.get('read')?.setValue(true);
    this.readProgressGroup.get('notRead')?.setValue(true);
    this.readProgressGroup.get('inProgress')?.setValue(true);
    this.sortGroup.get('sortField')?.setValue(SortField.SortName);
    this.isAscendingSort = true;
    this.resetTypeaheads.next(true);

    this.applyFilter.emit(this.filter);
    this.updateApplied++;
  }

  apply() {
    this.applyFilter.emit(this.filter);
    this.updateApplied++;
  }

}
