import {inject, Injectable} from '@angular/core';
import {Params, Router} from '@angular/router';
import {allSeriesSortFields, SortField} from 'src/app/_models/metadata/series-filter';
import {MetadataService} from "../../_services/metadata.service";
import {FilterV2} from "../../_models/metadata/v2/filter-v2";
import {FilterCombination} from "../../_models/metadata/v2/filter-combination";
import {allSeriesFilterFields, FilterField} from "../../_models/metadata/v2/filter-field";
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
  SeriesFilterSettings,
  ValidFilterEntity
} from "../../metadata-filter/filter-settings";
import {SortFieldPipe} from "../../_pipes/sort-field.pipe";
import {GenericFilterFieldPipe} from "../../_pipes/generic-filter-field.pipe";
import {TranslocoService} from "@jsverse/transloco";


@Injectable({
    providedIn: 'root'
})
export class FilterUtilitiesService {

  private readonly router = inject(Router);
  private readonly metadataService = inject(MetadataService);
  private readonly http = inject(HttpClient);
  private readonly translocoService = inject(TranslocoService);

  private readonly sortFieldPipe = new SortFieldPipe(this.translocoService);
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
   * @param filter
   * @param comparison
   * @param value
   */
  applyFilter(page: Array<any>, filter: FilterField, comparison: FilterComparison, value: string) {
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
        sortField: SortField.SortName
      },
    };
  }

  /**
   * Returns the Sort Fields for the Metadata filter based on the entity.
   * @param type
   */
  getSortFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return allSeriesSortFields.map(f => {
          return {title: this.sortFieldPipe.transform(f, type), value: f};
        }).sort((a, b) => a.title.localeCompare(b.title)) as unknown as {title: string, value: T}[];
      case 'person':
        return allPersonSortFields.map(f => {
          return {title: this.sortFieldPipe.transform(f, type), value: f};
        }).sort((a, b) => a.title.localeCompare(b.title)) as unknown as {title: string, value: T}[];
      default:
        return [] as {title: string, value: T}[];
    }
  }

  /**
   * Returns the Filter Fields for the Metadata filter based on the entity.
   * @param type
   */
  getFilterFields<T extends number>(type: ValidFilterEntity): {title: string, value: T}[] {
    switch (type) {
      case 'series':
        return allSeriesFilterFields.map(f => {
          return {title: this.genericFilterFieldPipe.transform(f, type), value: f};
        }).sort((a, b) => a.title.localeCompare(b.title)) as unknown as {title: string, value: T}[];
      case 'person':
        return allPersonFilterFields.map(f => {
          return {title: this.genericFilterFieldPipe.transform(f, type), value: f};
        }).sort((a, b) => a.title.localeCompare(b.title)) as unknown as {title: string, value: T}[];
      default:
        return [] as {title: string, value: T}[];
    }
  }

  /**
   * Returns the default field for the Series or Person entity aka what should be there if there are no statements
   * @param type
   */
  getDefaultFilterField<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return FilterField.SeriesName as unknown as T;
      case 'person':
        return PersonFilterField.Role as unknown as T;
    }
  }

  /**
   * Returns the appropriate Dropdown Fields based on the entity type
   * @param type
   */
  getDropdownFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.PublicationStatus, FilterField.Languages, FilterField.AgeRating,
          FilterField.Translators, FilterField.Characters, FilterField.Publisher,
          FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
          FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
          FilterField.Writers, FilterField.Genres, FilterField.Libraries,
          FilterField.Formats, FilterField.CollectionTags, FilterField.Tags,
          FilterField.Imprint, FilterField.Team, FilterField.Location
        ] as unknown as T[];
      case 'person':
        return [
          PersonFilterField.Role
        ] as unknown as T[];
    }
  }

  /**
   * Returns the applicable String fields
   * @param type
   */
  getStringFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.SeriesName, FilterField.Summary, FilterField.Path, FilterField.FilePath, PersonFilterField.Name
        ] as unknown as T[];
      case 'person':
        return [
          PersonFilterField.Name
        ] as unknown as T[];
    }
  }

  getNumberFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.ReadTime, FilterField.ReleaseYear, FilterField.ReadProgress,
          FilterField.UserRating, FilterField.AverageRating, FilterField.ReadLast
        ] as unknown as T[];
      case 'person':
        return [
          PersonFilterField.ChapterCount, PersonFilterField.SeriesCount
        ] as unknown as T[];
    }
  }

  getBooleanFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.WantToRead
        ] as unknown as T[];
      case 'person':
        return [

        ] as unknown as T[];
    }
  }

  getDateFields<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.ReadingDate
        ] as unknown as T[];
      case 'person':
        return [

        ] as unknown as T[];
    }
  }

  getNumberFieldsThatIncludeDateComparisons<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.ReleaseYear
        ] as unknown as T[];
      case 'person':
        return [

        ] as unknown as T[];
    }
  }

  getDropdownFieldsThatIncludeDateComparisons<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.AgeRating
        ] as unknown as T[];
      case 'person':
        return [

        ] as unknown as T[];
    }
  }

  getDropdownFieldsWithoutMustContains<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.Libraries, FilterField.Formats, FilterField.AgeRating, FilterField.PublicationStatus
        ] as unknown as T[];
      case 'person':
        return [

        ] as unknown as T[];
    }
  }

  getDropdownFieldsThatIncludeNumberComparisons<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.AgeRating
        ] as unknown as T[];
      case 'person':
        return [

        ] as unknown as T[];
    }
  }

  getFieldsThatShouldIncludeIsEmpty<T extends number>(type: ValidFilterEntity) {
    switch (type) {
      case 'series':
        return [
          FilterField.Summary, FilterField.UserRating, FilterField.Genres,
          FilterField.CollectionTags, FilterField.Tags, FilterField.ReleaseYear,
          FilterField.Translators, FilterField.Characters, FilterField.Publisher,
          FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
          FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
          FilterField.Writers, FilterField.Imprint, FilterField.Team,
          FilterField.Location
        ] as unknown as T[];
      case 'person':
        return [] as unknown as T[];
    }
  }

  getDefaultSettings(entityType: ValidFilterEntity | "other" | undefined): FilterSettingsBase<any, any> {
    if (entityType === 'other' || entityType === undefined) {
      // It doesn't matter, return series type
      return new SeriesFilterSettings();
    }

    if (entityType == 'series') return new SeriesFilterSettings();
    if (entityType == 'person') return new PersonFilterSettings();

    return new SeriesFilterSettings();
  }
}
