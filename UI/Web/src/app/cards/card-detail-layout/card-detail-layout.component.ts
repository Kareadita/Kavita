import { Component, ContentChild, EventEmitter, Input, OnInit, Output, TemplateRef } from '@angular/core';
import { FormGroup, FormControl } from '@angular/forms';
import { of } from 'rxjs';
import { map } from 'rxjs/operators';
import { TypeaheadSettings } from 'src/app/typeahead/typeahead-settings';
import { Genre } from 'src/app/_models/genre';
import { Library } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Pagination } from 'src/app/_models/pagination';
import { Person, PersonRole } from 'src/app/_models/person';
import { FilterItem, mangaFormatFilters, ReadStatus, SeriesFilter } from 'src/app/_models/series-filter';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { LibraryService } from 'src/app/_services/library.service';
import { MetadataService } from 'src/app/_services/metadata.service';
import { SeriesService } from 'src/app/_services/series.service';

const FILTER_PAG_REGEX = /[^0-9]/g;

export enum FilterAction {
  /**
   * If an option is selected on a multi select component
   */
  Added = 0,
  /**
   * If an option is unselected on a multi select component
   */
  Removed = 1,
  /**
   * If an option is selected on a single select component
   */
  Selected = 2
}

export interface UpdateFilterEvent {
  //filterItem: FilterItem;
  //action: FilterAction; // Do I need this?

  formatFilter?: FilterItem<MangaFormat>[];
}

const ANIMATION_SPEED = 300;

@Component({
  selector: 'app-card-detail-layout',
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss']
})
export class CardDetailLayoutComponent implements OnInit {

  @Input() header: string = '';
  @Input() isLoading: boolean = false; 
  @Input() items: any[] = [];
  @Input() pagination!: Pagination;
  /**
   * Any actions to exist on the header for the parent collection (library, collection)
   */
  @Input() actions: ActionItem<any>[] = [];
  @Input() trackByIdentity!: (index: number, item: any) => string;
  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() pageChange: EventEmitter<Pagination> = new EventEmitter();
  @Output() applyFilter: EventEmitter<SeriesFilter> = new EventEmitter();
  
  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;
  

  formatSettings: TypeaheadSettings<FilterItem<MangaFormat>> = new TypeaheadSettings();
  librarySettings: TypeaheadSettings<FilterItem<Library>> = new TypeaheadSettings();
  genreSettings: TypeaheadSettings<FilterItem<Genre>> = new TypeaheadSettings();
  personSettings: TypeaheadSettings<FilterItem<Person>> = new TypeaheadSettings();

  /**
   * Controls the visiblity of extended controls that sit below the main header.
   */
  filteringCollapsed: boolean = true;

  filter!: SeriesFilter;
  libraries: Array<FilterItem<Library>> = [];
  genres: Array<FilterItem<Genre>> = [];
  people: Array<FilterItem<Person>> = [];

  updateApplied: number = 0;

  get PersonRole(): typeof PersonRole {
    return PersonRole;
  }

  constructor(private libraryService: LibraryService, private metadataService: MetadataService, private seriesService: SeriesService) {
    this.filter = this.seriesService.createSeriesFilter();
  }

  ngOnInit(): void {
    // BUG: TrackByIdentity isn't working
    this.trackByIdentity = (index: number, item: any) => `${this.header}_${this.pagination?.currentPage}_${this.updateApplied}`;
    this.setupFormatTypeahead();
    

    this.metadataService.getAllGenres().subscribe(genres => {
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
    this.formatSettings.savedData = mangaFormatFilters;
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
    this.librarySettings.savedData = this.libraries;
    console.log('libraries: ', this.libraries);
  }

  setupGenreTypeahead() {
    this.genreSettings.minCharacters = 0;
    this.genreSettings.multiple = true;
    this.genreSettings.id = 'genres';
    this.genreSettings.unique = true;
    this.genreSettings.addIfNonExisting = false;
    this.genreSettings.fetchFn = (filter: string) => {
      return of (this.genres)
    };
    this.genreSettings.compareFn = (options: FilterItem<Genre>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }
    this.genreSettings.savedData = this.genres;
  }

  setupPersonTypeahead() {
    this.genreSettings.minCharacters = 0;
    this.genreSettings.multiple = true;
    this.genreSettings.id = 'genres';
    this.genreSettings.unique = true;
    this.genreSettings.addIfNonExisting = false;
    this.genreSettings.fetchFn = (filter: string) => {
      return of (this.genres)
    };
    this.genreSettings.compareFn = (options: FilterItem<Genre>[], filter: string) => {
      const f = filter.toLowerCase();
      return options.filter(m => m.title.toLowerCase() === f);
    }
    this.genreSettings.savedData = this.genres;
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

  apply() {
    this.applyFilter.emit(this.filter);
    this.updateApplied++;
  }

}
