import {inject, Injectable, PipeTransform} from '@angular/core';
import {Params, Router} from '@angular/router';
import {allSeriesSortFields, SeriesSortField} from 'src/app/_models/metadata/series-filter';
import {MetadataService} from "../../_services/metadata.service";
import {FilterV2} from "../../_models/metadata/v2/filter-v2";
import {FilterCombination} from "../../_models/metadata/v2/filter-combination";
import {allSeriesFilterFields, SeriesFilterField} from "../../_models/metadata/v2/series-filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {HttpClient} from "@angular/common/http";
import {TextResonse} from "../../_types/text-response";
import {environment} from "../../../environments/environment";
import {map, tap} from "rxjs/operators";
import {switchMap} from "rxjs";
import {allPersonFilterFields, PersonFilterField} from "../../_models/metadata/v2/person-filter-field";
import {allPersonSortFields} from "../../_models/metadata/v2/person-sort-field";
import {
  FilterSettingsBase,
  PersonFilterSettings,
  ReadingListFilterSettings,
  SeriesFilterSettings,
  ValidFilterEntity
} from "../../metadata-filter/filter-settings";
import {SortFieldPipe} from "../../_pipes/sort-field.pipe";
import {GenericFilterFieldPipe} from "../../_pipes/generic-filter-field.pipe";
import {
  allAnnotationsFilterFields,
  allAnnotationsSortFields,
  AnnotationsFilterField
} from "../../_models/metadata/v2/annotations-filter";
import {allReadingListFilterFields, ReadingListFilterField} from "../../_models/metadata/v2/reading-list-filter-field";
import {allReadingListSortFields, ReadingListSortField} from "../../_models/metadata/v2/reading-list-sort-field";

export interface FieldOption<T extends number> {
  title: string,
  value: T,
}

@Injectable({
    providedIn: 'root'
})
export class FilterUtilitiesService {

  private readonly router = inject(Router);
  private readonly metadataService = inject(MetadataService);
  private readonly http = inject(HttpClient);

  private readonly sortFieldPipe = new SortFieldPipe();
  private readonly genericFilterFieldPipe = new GenericFilterFieldPipe();

  private readonly apiUrl = environment.apiUrl;

  encodeFilter(filter: FilterV2 | undefined) {
    return this.http.post<string>(this.apiUrl + 'filter/encode', filter, TextResonse);
  }

  decodeFilter(encodedFilter: string) {
    return this.http.post<FilterV2>(this.apiUrl + 'filter/decode', {encodedFilter}).pipe(map(filter => {
      if (filter == null) {
        filter = this.metadataService.createDefaultFilterDto('series');
        filter.statements.push(this.metadataService.createDefaultFilterStatement('series'));
      }

      return filter;
    }))
  }

  /**
   * Encodes the filter and patches into the url
   * @param filter
   */
  updateUrlFromFilter(filter: FilterV2 | undefined) {
    return this.encodeFilter(filter).pipe(tap(encodedFilter => {
      window.history.replaceState(window.location.href, '', window.location.href.split('?')[0]+ '?' + encodedFilter);
    }));
  }

  /**
   * Applies and redirects to the passed page with the filter encoded (Series only)
   * @param page
   * @param filter - One of the filter fieldsFix
   * @param comparison
   * @param value
   */
  applyFilter(page: Array<any>, filter: SeriesFilterField | ReadingListFilterField | PersonFilterField | AnnotationsFilterField, comparison: FilterComparison, value: string) {
    const dto = this.metadataService.createDefaultFilterDto('series');
    dto.statements.push(this.metadataService.createFilterStatement(filter, comparison, value + ''));

    return this.encodeFilter(dto).pipe(switchMap(encodedFilter => {
      return this.router.navigateByUrl(page.join('/') + '?' + encodedFilter);
    }));
  }

  /**
   *  (Series only)
   * @param page
   * @param filter
   * @param extraParams
   */
  applyFilterWithParams(page: Array<any>, filter: FilterV2<any>, extraParams: Params) {
    return this.encodeFilter(filter).pipe(switchMap(encodedFilter => {
      let url = page.join('/') + '?' + encodedFilter;
      url += Object.keys(extraParams).map(k => `&${k}=${extraParams[k]}`).join('');

      return this.router.navigateByUrl(url, extraParams);
    }));
  }


  createPersonV2Filter(): FilterV2<PersonFilterField> {
    return {
      combination: FilterCombination.And,
      statements: [],
      limitTo: 0,
      sortOptions: {
        isAscending: true,
        sortField: SeriesSortField.SortName
      },
    };
  }

  createReadingListV2Filter(): FilterV2<ReadingListFilterField> {
    return {
      combination: FilterCombination.And,
      statements: [],
      limitTo: 0,
      sortOptions: {
        isAscending: true,
        sortField: ReadingListSortField.Title
      },
    };
  }

  /**
   * Returns the Sort Fields for the Metadata filter based on the entity.
   * @param type
   */
  getSortFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return this.translateAndSort(type, this.sortFieldPipe, allAnnotationsSortFields) as FieldOption<T>[];
      case 'series':
        return this.translateAndSort(type, this.sortFieldPipe, allSeriesSortFields) as FieldOption<T>[];
      case 'person':
        return this.translateAndSort(type, this.sortFieldPipe, allPersonSortFields) as FieldOption<T>[];
      case 'readinglist':
        return this.translateAndSort(type, this.sortFieldPipe, allReadingListSortFields) as FieldOption<T>[];
    }
  }

  /**
   * Returns the Filter Fields for the Metadata filter based on the entity.
   * @param type
   */
  getFilterFields<T extends number>(type: ValidFilterEntity): FieldOption<T>[] {
    switch (type) {
      case 'annotation':
        return this.translateAndSort(type, this.genericFilterFieldPipe, allAnnotationsFilterFields) as FieldOption<T>[];
      case 'series':
        return this.translateAndSort(type, this.genericFilterFieldPipe, allSeriesFilterFields) as FieldOption<T>[];
      case 'person':
        return this.translateAndSort(type, this.genericFilterFieldPipe, allPersonFilterFields) as FieldOption<T>[];
      case 'readinglist':
        return this.translateAndSort(type, this.genericFilterFieldPipe, allReadingListFilterFields) as FieldOption<T>[];
    }
  }

  private translateAndSort<T extends number>(type: ValidFilterEntity, pipe: PipeTransform, items: T[]): FieldOption<T>[] {
    return items
      .map(item => {
        return {title: pipe.transform(item, type), value: item};
      })
      .sort((a, b) => {
        return a.title.localeCompare(b.title);
      });
  }

  /**
   * Returns the default field for the Series or Person entity aka what should be there if there are no statements
   * @param type
   */
  getDefaultFilterField<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return AnnotationsFilterField.Owner as unknown as T;
      case 'series':
        return SeriesFilterField.SeriesName as unknown as T;
      case 'person':
        return PersonFilterField.Role as unknown as T;
      case 'readinglist':
        return ReadingListFilterField.Title as unknown as T;
    }
  }

  /**
   * Returns the appropriate Dropdown Fields based on the entity type
   * @param type
   */
  getDropdownFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [
          AnnotationsFilterField.Owner, AnnotationsFilterField.Library,
          AnnotationsFilterField.HighlightSlots, AnnotationsFilterField.Series,
          AnnotationsFilterField.LikedBy,
        ] as T[];
      case 'series':
        return [
          SeriesFilterField.PublicationStatus, SeriesFilterField.Languages, SeriesFilterField.AgeRating,
          SeriesFilterField.Translators, SeriesFilterField.Characters, SeriesFilterField.Publisher,
          SeriesFilterField.Editor, SeriesFilterField.CoverArtist, SeriesFilterField.Letterer,
          SeriesFilterField.Colorist, SeriesFilterField.Inker, SeriesFilterField.Penciller,
          SeriesFilterField.Writers, SeriesFilterField.Genres, SeriesFilterField.Libraries,
          SeriesFilterField.Formats, SeriesFilterField.CollectionTags, SeriesFilterField.Tags,
          SeriesFilterField.Imprint, SeriesFilterField.Team, SeriesFilterField.Location
        ] as unknown as T[];
      case 'person':
        return [
          PersonFilterField.Role, PersonFilterField.Library,
        ] as unknown as T[];
      case 'readinglist':
        return [
          ReadingListFilterField.Writer, ReadingListFilterField.Artist, ReadingListFilterField.Tags
        ] as unknown as T[];
    }
  }

  /**
   * Returns the applicable String fields
   * @param type
   */
  getStringFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [
          AnnotationsFilterField.Comment, AnnotationsFilterField.Selection,
        ] as T[];
      case 'series':
        return [
          SeriesFilterField.SeriesName, SeriesFilterField.Summary, SeriesFilterField.Path, SeriesFilterField.FilePath, SeriesFilterField.FileSize,
        ] as unknown as T[];
      case 'person':
        return [
          PersonFilterField.Name
        ] as unknown as T[];
      case 'readinglist':
        return [
          ReadingListFilterField.Title
        ] as unknown as T[];
    }
  }

  getNumberFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [
          AnnotationsFilterField.Likes,
        ] as T[];
      case 'series':
        return [
          SeriesFilterField.ReadTime, SeriesFilterField.ReleaseYear, SeriesFilterField.ReadProgress,
          SeriesFilterField.UserRating, SeriesFilterField.AverageRating, SeriesFilterField.ReadLast
        ] as unknown as T[];
      case 'person':
        return [
          PersonFilterField.ChapterCount, PersonFilterField.SeriesCount
        ] as unknown as T[];
      case 'readinglist':
        return [
          ReadingListFilterField.ItemCount
        ] as unknown as T[];
    }
  }

  getBooleanFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [
          AnnotationsFilterField.Spoiler,
        ] as T[];
      case 'series':
        return [
          SeriesFilterField.WantToRead
        ] as unknown as T[];
      case 'person':
        return [] as unknown as T[];
      case 'readinglist':
        return [] as unknown as T[];
    }
  }

  getDateFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [] as T[];
      case 'series':
        return [
          SeriesFilterField.ReadingDate
        ] as unknown as T[];
      case 'person':
        return [] as unknown as T[];
      case 'readinglist':
        return [] as unknown as T[];
    }
  }

  getNumberFieldsThatIncludeDateComparisons<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [] as T[];
      case 'series':
        return [
          SeriesFilterField.ReleaseYear
        ] as unknown as T[];
      case 'person':
        return [] as unknown as T[];
      case 'readinglist':
        return [
          ReadingListFilterField.ReleaseYear
        ] as unknown as T[];
    }
  }

  getDropdownFieldsThatIncludeDateComparisons<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [] as T[];
      case 'series':
        return [
          SeriesFilterField.AgeRating
        ] as unknown as T[];
      case 'person':
        return [] as unknown as T[];
      case 'readinglist':
        return [] as unknown as T[];
    }
  }

  getDropdownFieldsWithoutMustContains<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [] as T[];
      case 'series':
        return [
          SeriesFilterField.Libraries, SeriesFilterField.Formats, SeriesFilterField.AgeRating, SeriesFilterField.PublicationStatus
        ] as unknown as T[];
      case 'person':
        return [] as unknown as T[];
      case 'readinglist':
        return [] as unknown as T[];
    }
  }

  getDropdownFieldsThatIncludeNumberComparisons<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [] as T[];
      case 'series':
        return [
          SeriesFilterField.AgeRating
        ] as unknown as T[];
      case 'person':
        return [] as unknown as T[];
      case 'readinglist':
        return [] as unknown as T[];
    }
  }

  getFieldsThatShouldIncludeIsEmpty<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'annotation':
        return [] as T[];
      case 'series':
        return [
          SeriesFilterField.Summary, SeriesFilterField.UserRating, SeriesFilterField.Genres,
          SeriesFilterField.CollectionTags, SeriesFilterField.Tags, SeriesFilterField.ReleaseYear,
          SeriesFilterField.Translators, SeriesFilterField.Characters, SeriesFilterField.Publisher,
          SeriesFilterField.Editor, SeriesFilterField.CoverArtist, SeriesFilterField.Letterer,
          SeriesFilterField.Colorist, SeriesFilterField.Inker, SeriesFilterField.Penciller,
          SeriesFilterField.Writers, SeriesFilterField.Imprint, SeriesFilterField.Team,
          SeriesFilterField.Location
        ] as unknown as T[];
      case 'person':
        return [] as unknown as T[];
      case 'readinglist':
        return [ReadingListFilterField.Tags] as unknown as T[];
    }
  }

  getDefaultSettings(entityType: ValidFilterEntity | 'other' | undefined): FilterSettingsBase<any, any> {
    if (entityType === 'other' || entityType === undefined) {
      // It doesn't matter, return series type
      return new SeriesFilterSettings();
    }

    if (entityType == 'series') return new SeriesFilterSettings();
    if (entityType == 'person') return new PersonFilterSettings();
    if (entityType == 'readinglist') return new ReadingListFilterSettings();

    return new SeriesFilterSettings();
  }

  /**
   * Fully override which comparisons a field offers. This MUST return at least one FilterComparison
   */
  getCustomComparisons<T extends number>(entityType: ValidFilterEntity, field: T): FilterComparison[] | null {
    switch (entityType) {
      case 'series':
        switch (field) {
          case SeriesFilterField.FileSize:
            return [
              FilterComparison.Equal, FilterComparison.GreaterThan, FilterComparison.GreaterThanEqual,
              FilterComparison.LessThan, FilterComparison.LessThanEqual
            ]
        }
    }

    return null;
  }
}
