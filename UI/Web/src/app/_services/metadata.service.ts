import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {tap} from 'rxjs/operators';
import {of} from 'rxjs';
import {environment} from 'src/environments/environment';
import {Genre} from '../_models/metadata/genre';
import {AgeRatingDto} from '../_models/metadata/age-rating-dto';
import {Language} from '../_models/metadata/language';
import {PublicationStatusDto} from '../_models/metadata/publication-status-dto';
import {Person, PersonRole} from '../_models/metadata/person';
import {Tag} from '../_models/tag';
import {FilterComparison} from '../_models/metadata/v2/filter-comparison';
import {FilterField} from '../_models/metadata/v2/filter-field';
import {Router} from "@angular/router";
import {SortField} from "../_models/metadata/series-filter";
import {FilterCombination} from "../_models/metadata/v2/filter-combination";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {FilterStatement} from "../_models/metadata/v2/filter-statement";
import {SeriesDetailPlus} from "../_models/series-detail/series-detail-plus";
import {LibraryType} from "../_models/library/library";

@Injectable({
  providedIn: 'root'
})
export class MetadataService {

  baseUrl = environment.apiUrl;
  private validLanguages: Array<Language> = [];

  constructor(private httpClient: HttpClient) { }

  getSeriesMetadataFromPlus(seriesId: number, libraryType: LibraryType) {
    return this.httpClient.get<SeriesDetailPlus | null>(this.baseUrl + 'metadata/series-detail-plus?seriesId=' + seriesId + '&libraryType=' + libraryType);
  }

  getAllAgeRatings(libraries?: Array<number>) {
    let method = 'metadata/age-ratings'
    if (libraries != undefined && libraries.length > 0) {
      method += '?libraryIds=' + libraries.join(',');
    }
    return this.httpClient.get<Array<AgeRatingDto>>(this.baseUrl + method);
  }

  getAllPublicationStatus(libraries?: Array<number>) {
    let method = 'metadata/publication-status'
    if (libraries != undefined && libraries.length > 0) {
      method += '?libraryIds=' + libraries.join(',');
    }
    return this.httpClient.get<Array<PublicationStatusDto>>(this.baseUrl + method);
  }

  getAllTags(libraries?: Array<number>) {
    let method = 'metadata/tags'
    if (libraries != undefined && libraries.length > 0) {
      method += '?libraryIds=' + libraries.join(',');
    }
    return this.httpClient.get<Array<Tag>>(this.baseUrl + method);
  }

  getAllGenres(libraries?: Array<number>) {
    let method = 'metadata/genres'
    if (libraries != undefined && libraries.length > 0) {
      method += '?libraryIds=' + libraries.join(',');
    }
    return this.httpClient.get<Array<Genre>>(this.baseUrl + method);
  }

  getAllLanguages(libraries?: Array<number>) {
    let method = 'metadata/languages'
    if (libraries != undefined && libraries.length > 0) {
      method += '?libraryIds=' + libraries.join(',');
    }
    return this.httpClient.get<Array<Language>>(this.baseUrl + method);
  }


  /**
   * All the potential language tags there can be
   */
  getAllValidLanguages() {
    if (this.validLanguages != undefined && this.validLanguages.length > 0) {
      return of(this.validLanguages);
    }
    return this.httpClient.get<Array<Language>>(this.baseUrl + 'metadata/all-languages')
      .pipe(tap(l => this.validLanguages = l));
  }

  getAllPeople(libraries?: Array<number>) {
    let method = 'metadata/people'
    if (libraries != undefined && libraries.length > 0) {
      method += '?libraryIds=' + libraries.join(',');
    }
    return this.httpClient.get<Array<Person>>(this.baseUrl + method);
  }

  getAllPeopleByRole(role: PersonRole) {
    return this.httpClient.get<Array<Person>>(this.baseUrl + 'metadata/people-by-role?role=' + role);
  }

  createDefaultFilterDto(): SeriesFilterV2 {
    return {
      statements: [] as FilterStatement[],
      combination: FilterCombination.And,
      limitTo: 0,
      sortOptions: {
        isAscending: true,
        sortField: SortField.SortName
      }
    };
  }

  createDefaultFilterStatement(field: FilterField = FilterField.SeriesName, comparison = FilterComparison.Equal, value = '') {
    return {
      comparison: comparison,
      field: field,
      value: value
    };
  }

  updateFilter(arr: Array<FilterStatement>, index: number, filterStmt: FilterStatement) {
    arr[index].comparison = filterStmt.comparison;
    arr[index].field = filterStmt.field;
    arr[index].value = filterStmt.value ? filterStmt.value + '' : '';
  }
}
