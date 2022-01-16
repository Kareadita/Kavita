import { Component, ContentChild, EventEmitter, Input, OnDestroy, OnInit, Output, TemplateRef } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { forkJoin, Observable, of, ReplaySubject, Subject } from 'rxjs';
import { map, takeUntil } from 'rxjs/operators';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { TypeaheadSettings } from 'src/app/typeahead/typeahead-settings';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { Genre } from 'src/app/_models/genre';
import { Library } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { AgeRatingDto } from 'src/app/_models/metadata/age-rating-dto';
import { Language } from 'src/app/_models/metadata/language';
import { PublicationStatusDto } from 'src/app/_models/metadata/publication-status-dto';
import { Pagination } from 'src/app/_models/pagination';
import { Person, PersonRole } from 'src/app/_models/person';
import { FilterItem, mangaFormatFilters, SeriesFilter, SortField } from 'src/app/_models/series-filter';
import { Tag } from 'src/app/_models/tag';
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
  sortDisabled = false;
  ageRatingDisabled = false;
  tagsDisabled = false;
  languageDisabled = false;
  publicationStatusDisabled = false;
  presets: SeriesFilter | undefined;
  /**
   * Should the filter section be open by default
   */
  openByDefault = false;
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
  ageRatingSettings: TypeaheadSettings<FilterItem<AgeRatingDto>> = new TypeaheadSettings();
  publicationStatusSettings: TypeaheadSettings<FilterItem<PublicationStatusDto>> = new TypeaheadSettings();
  tagsSettings: TypeaheadSettings<FilterItem<Tag>> = new TypeaheadSettings();
  languageSettings: TypeaheadSettings<FilterItem<Language>> = new TypeaheadSettings();
  peopleSettings: {[PersonRole: string]: TypeaheadSettings<Person>} = {};
  resetTypeaheads: Subject<boolean> = new ReplaySubject(1);

  /**
   * Controls the visiblity of extended controls that sit below the main header.
   */
  filteringCollapsed: boolean = true;

  filter!: SeriesFilter;
  libraries: Array<FilterItem<Library>> = [];
  genres: Array<FilterItem<Genre>> = [];
  persons: Array<FilterItem<Person>> = [];


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

    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettings();
    }

    this.libraryService.getLibrariesForMember().subscribe(libs => {
      this.libraries = libs.map(lib => {
        return {
          title: lib.name,
          value: lib,
          selected: true,
        }
      });
      this.setupTypeaheads();
    });

    
  }

  ngOnDestroy() {
    this.onDestory.next();
    this.onDestory.complete();
  }

  setupTypeaheads() {
    this.setupFormatTypeahead();
    this.setupLibraryTypeahead();
    this.setupCollectionTagTypeahead();
    this.setupPersonTypeahead();
    this.setupAgeRatingSettings();
    this.setupPublicationStatusSettings();
    this.setupTagSettings();
    this.setupLanguageSettings();
    this.setupGenreTypeahead();

    this.resetTypeaheads.next(true);
    if (this.filterSettings.openByDefault) {
      this.filteringCollapsed = false;
      this.apply();
    }
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

    if (this.filterSettings.presets?.formats && this.filterSettings.presets?.formats.length > 0) {
      this.formatSettings.savedData = mangaFormatFilters.filter(item => this.filterSettings.presets?.formats.includes(item.value));
      
      this.filter.formats = this.formatSettings.savedData.map(item => item.value);
      this.resetTypeaheads.next(true);
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

    if (this.filterSettings.presets?.libraries && this.filterSettings.presets?.libraries.length > 0) {
      this.librarySettings.savedData = this.libraries.filter(item => this.filterSettings.presets?.libraries.includes(item.value.id));
      
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
      return this.metadataService.getAllGenres(this.filter.libraries).pipe(map(genres => {
        // TODO: Need to remove already selected genres (typeahead isn't doing automatically)
        return genres.map(genre => {
          return {
            title: genre.title,
            value: genre,
            selected: false,
          }
        })
      }));
    };
    this.genreSettings.compareFn = (options: FilterItem<Genre>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }

    if (this.filterSettings.presets?.genres && this.filterSettings.presets?.genres.length > 0) {
      this.genreSettings.fetchFn('').subscribe(genres => {
        this.genreSettings.savedData = genres.filter(item => this.filterSettings.presets?.genres.includes(item.value.id));
        this.filter.genres = this.genreSettings.savedData.map(item => item.value.id);
        this.resetTypeaheads.next(true);
      });
    }
  }

  setupAgeRatingSettings() {
    this.ageRatingSettings.minCharacters = 0;
    this.ageRatingSettings.multiple = true;
    this.ageRatingSettings.id = 'age-rating';
    this.ageRatingSettings.unique = true;
    this.ageRatingSettings.addIfNonExisting = false;
    this.ageRatingSettings.fetchFn = (filter: string) => {
      return this.metadataService.getAllAgeRatings(this.filter.libraries).pipe(map(ratings => {
        return ratings.map(rating => {
          return {
            title: rating.title,
            value: rating,
            selected: false,
          }
        })
      }));
    };
    this.ageRatingSettings.compareFn = (options: FilterItem<AgeRatingDto>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f && this.utilityService.filter(m.title, filter));
    }

    if (this.filterSettings.presets?.ageRating && this.filterSettings.presets?.ageRating.length > 0) {
      this.ageRatingSettings.fetchFn('').subscribe(rating => {
        this.ageRatingSettings.savedData = rating.filter(item => this.filterSettings.presets?.ageRating.includes(item.value.value));
        this.filter.ageRating = this.ageRatingSettings.savedData.map(item => item.value.value);
        this.resetTypeaheads.next(true);
      });
    }
  }

  setupPublicationStatusSettings() {
    this.publicationStatusSettings.minCharacters = 0;
    this.publicationStatusSettings.multiple = true;
    this.publicationStatusSettings.id = 'publication-status';
    this.publicationStatusSettings.unique = true;
    this.publicationStatusSettings.addIfNonExisting = false;
    this.publicationStatusSettings.fetchFn = (filter: string) => {
      return this.metadataService.getAllPublicationStatus(this.filter.libraries).pipe(map(statuses => {
        return statuses.map(status => {
          return {
            title: status.title,
            value: status,
            selected: false,
          }
        })
      }));
    };
    this.publicationStatusSettings.compareFn = (options: FilterItem<PublicationStatusDto>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f && this.utilityService.filter(m.title, filter));
    }

    if (this.filterSettings.presets?.publicationStatus && this.filterSettings.presets?.publicationStatus.length > 0) {
      this.publicationStatusSettings.fetchFn('').subscribe(statuses => {
        this.publicationStatusSettings.savedData = statuses.filter(item => this.filterSettings.presets?.publicationStatus.includes(item.value.value));
        this.filter.publicationStatus = this.publicationStatusSettings.savedData.map(item => item.value.value);
        this.resetTypeaheads.next(true);
      });
    }
  }

  setupTagSettings() {
    this.tagsSettings.minCharacters = 0;
    this.tagsSettings.multiple = true;
    this.tagsSettings.id = 'tags';
    this.tagsSettings.unique = true;
    this.tagsSettings.addIfNonExisting = false;
    this.tagsSettings.fetchFn = (filter: string) => {
      return this.metadataService.getAllTags(this.filter.libraries).pipe(map(tags => {
        return tags.map(tag => {
          return {
            title: tag.title,
            value: tag,
            selected: false,
          }
        })
      }));
    };
    this.tagsSettings.compareFn = (options: FilterItem<Tag>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f && this.utilityService.filter(m.title, filter));
    }

    if (this.filterSettings.presets?.tags && this.filterSettings.presets?.tags.length > 0) {
      this.tagsSettings.fetchFn('').subscribe(tags => {
        this.tagsSettings.savedData = tags.filter(item => this.filterSettings.presets?.tags.includes(item.value.id));
        this.filter.tags = this.tagsSettings.savedData.map(item => item.value.id);
        this.resetTypeaheads.next(true);
      });
    }
  }

  setupLanguageSettings() {
    this.languageSettings.minCharacters = 0;
    this.languageSettings.multiple = true;
    this.languageSettings.id = 'languages';
    this.languageSettings.unique = true;
    this.languageSettings.addIfNonExisting = false;
    this.languageSettings.fetchFn = (filter: string) => {
      return this.metadataService.getAllLanguages(this.filter.libraries).pipe(map(languages => {
        return languages.map(lang => {
          return {
            title: lang.title,
            value: lang,
            selected: false,
          }
        })
      }));
    };
    this.languageSettings.compareFn = (options: FilterItem<Language>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f && this.utilityService.filter(m.title, filter));
    }

    if (this.filterSettings.presets?.languages && this.filterSettings.presets?.languages.length > 0) {
      this.languageSettings.fetchFn('').subscribe(languages => {
        this.languageSettings.savedData = languages.filter(item => this.filterSettings.presets?.languages.includes(item.value.isoCode));
        this.filter.languages = this.languageSettings.savedData.map(item => item.value.isoCode);
        this.resetTypeaheads.next(true);
      });
    }
  }

  setupCollectionTagTypeahead() {
    this.collectionSettings.minCharacters = 0;
    this.collectionSettings.multiple = true;
    this.collectionSettings.id = 'collections';
    this.collectionSettings.unique = true;
    this.collectionSettings.addIfNonExisting = false;
    this.collectionSettings.fetchFn = (filter: string) => {
      return this.collectionTagService.allTags().pipe(map(tags => {
        return tags.map(lib => {
          return {
            title: lib.title,
            value: lib,
            selected: false,
          }
        });
      }));
    };
    this.collectionSettings.compareFn = (options: FilterItem<CollectionTag>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }

    if (this.filterSettings.presets?.collectionTags && this.filterSettings.presets?.collectionTags.length > 0) {
      this.collectionSettings.fetchFn('').subscribe(tags => {
        this.collectionSettings.savedData = tags.filter(item => this.filterSettings.presets?.collectionTags.includes(item.value.id));
        this.filter.collectionTags = this.collectionSettings.savedData.map(item => item.value.id);
        this.resetTypeaheads.next(true);
      });
    }
  }

  updateFromPreset(id: string, peopleFilterField: Array<any>, presetField: Array<any> | undefined, role: PersonRole) {
    const personSettings = this.createBlankPersonSettings(id, role)
    if (presetField && presetField.length > 0) {
      //const fetch = personSettings.fetchFn as ((filter: string) => Observable<FilterItem<Person>[]>);
      const fetch = personSettings.fetchFn as ((filter: string) => Observable<Person[]>);
      return fetch('').pipe(map(people => {
        personSettings.savedData = people.filter(item => presetField.includes(item.id));
        peopleFilterField = personSettings.savedData.map(item => item.id);
        this.resetTypeaheads.next(true);
        this.peopleSettings[role] = personSettings;
        //this.updatePersonFilters(personSettings.savedData as FilterItem<Person>[], role);
        this.updatePersonFilters(personSettings.savedData as Person[], role);
        return true;
      }));
    } else {
      this.peopleSettings[role] = personSettings;
      return of(true);
    }
  }

  setupPersonTypeahead() {
    this.peopleSettings = {};

    forkJoin([
      this.updateFromPreset('writers', this.filter.writers, this.filterSettings.presets?.writers, PersonRole.Writer),
      this.updateFromPreset('character', this.filter.character, this.filterSettings.presets?.character, PersonRole.Character),  
      this.updateFromPreset('colorist', this.filter.colorist, this.filterSettings.presets?.colorist, PersonRole.Colorist),
      this.updateFromPreset('cover-artist', this.filter.coverArtist, this.filterSettings.presets?.coverArtist, PersonRole.CoverArtist),
      this.updateFromPreset('editor', this.filter.editor, this.filterSettings.presets?.editor, PersonRole.Editor),
      this.updateFromPreset('inker', this.filter.inker, this.filterSettings.presets?.inker, PersonRole.Inker),
      this.updateFromPreset('letterer', this.filter.letterer, this.filterSettings.presets?.letterer, PersonRole.Letterer),
      this.updateFromPreset('penciller', this.filter.penciller, this.filterSettings.presets?.penciller, PersonRole.Penciller),
      this.updateFromPreset('publisher', this.filter.publisher, this.filterSettings.presets?.publisher, PersonRole.Publisher),
      this.updateFromPreset('translators', this.filter.translators, this.filterSettings.presets?.translators, PersonRole.Translator)
    ]).subscribe(results => {
      this.resetTypeaheads.next(true);
      this.apply();
    });
  }

  fetchPeople(role: PersonRole, filter: string) { //: Observable<FilterItem<Person>[]>
    return this.metadataService.getAllPeople(this.filter.libraries).pipe(map(people => {
      return people.filter(p => p.role == role && this.utilityService.filter(p.name, filter));
      // .map((p: Person) => {
      //   return {
      //     title: p.name,
      //     value: p,
      //     selected: false,
      //   }
      // });
    }));
  }

  createBlankPersonSettings(id: string, role: PersonRole) {
    var personSettings = new TypeaheadSettings<Person>();
    personSettings.minCharacters = 0;
    personSettings.multiple = true;
    personSettings.unique = true;
    personSettings.addIfNonExisting = false;
    personSettings.id = id;
    personSettings.compareFn = (options: Person[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.name.toLowerCase() === f);
    }
    personSettings.fetchFn = (filter: string) => {
      return this.fetchPeople(role, filter);
    };
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

  updateTagFilters(tags: FilterItem<Tag>[]) {
    this.filter.tags = tags.map(item => item.value.id) || [];
  }

  // updatePersonFilters(persons: FilterItem<Person>[], role: PersonRole) {
  //   switch (role) {
  //     case PersonRole.CoverArtist:
  //       this.filter.coverArtist = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Character:
  //       this.filter.character = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Colorist:
  //       this.filter.colorist = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Editor:
  //       this.filter.editor = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Inker:
  //       this.filter.inker = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Letterer:
  //       this.filter.letterer = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Penciller:
  //       this.filter.penciller = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Publisher:
  //       this.filter.publisher = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Writer:
  //       this.filter.writers = persons.map(p => p.value.id);
  //       break;
  //     case PersonRole.Translator:
  //       this.filter.translators = persons.map(p => p.value.id);

  //   }
  // }

  updatePersonFilters(persons: Person[], role: PersonRole) {
    switch (role) {
      case PersonRole.CoverArtist:
        this.filter.coverArtist = persons.map(p => p.id);
        break;
      case PersonRole.Character:
        this.filter.character = persons.map(p => p.id);
        break;
      case PersonRole.Colorist:
        this.filter.colorist = persons.map(p => p.id);
        break;
      case PersonRole.Editor:
        this.filter.editor = persons.map(p => p.id);
        break;
      case PersonRole.Inker:
        this.filter.inker = persons.map(p => p.id);
        break;
      case PersonRole.Letterer:
        this.filter.letterer = persons.map(p => p.id);
        break;
      case PersonRole.Penciller:
        this.filter.penciller = persons.map(p => p.id);
        break;
      case PersonRole.Publisher:
        this.filter.publisher = persons.map(p => p.id);
        break;
      case PersonRole.Writer:
        this.filter.writers = persons.map(p => p.id);
        break;
      case PersonRole.Translator:
        this.filter.translators = persons.map(p => p.id);

    }
  }

  updateCollectionFilters(tags: FilterItem<CollectionTag>[]) {
    this.filter.collectionTags = tags.map(item => item.value.id) || [];
  }

  updateRating(rating: any) {
    this.filter.rating = rating;
  }

  updateAgeRating(ratingDtos: FilterItem<AgeRatingDto>[]) {
    this.filter.ageRating = ratingDtos.map(item => item.value.value) || [];
  }

  updatePublicationStatus(dtos: FilterItem<PublicationStatusDto>[]) {
    this.filter.publicationStatus = dtos.map(item => item.value.value) || [];
  }

  updateLanguageRating(languages: FilterItem<Language>[]) {
    this.filter.languages = languages.map(item => item.value.isoCode) || [];
  }

  updateReadStatus(status: string) {
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
    if (this.filter.sortOptions === null) {
      this.filter.sortOptions = {
        isAscending: this.isAscendingSort,
        sortField: SortField.SortName
      }
    }

    this.filter.sortOptions.isAscending = this.isAscendingSort;
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
    // Apply any presets
    this.setupTypeaheads();
    this.resetTypeaheads.next(true);

    this.applyFilter.emit(this.filter);
    this.updateApplied++;
  }

  apply() {
    this.applyFilter.emit(this.filter);
    this.updateApplied++;
  }

}
