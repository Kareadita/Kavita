import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {map, tap} from 'rxjs/operators';
import {of, ReplaySubject, switchMap} from 'rxjs';
import {environment} from 'src/environments/environment';
import {Genre} from '../_models/metadata/genre';
import {AgeRating} from '../_models/metadata/age-rating';
import {AgeRatingDto} from '../_models/metadata/age-rating-dto';
import {Language} from '../_models/metadata/language';
import {PublicationStatusDto} from '../_models/metadata/publication-status-dto';
import {Person} from '../_models/metadata/person';
import {Tag} from '../_models/tag';
import {TextResonse} from '../_types/text-response';
import {FilterComparison} from '../_models/metadata/v2/filter-comparison';
import {FilterField} from '../_models/metadata/v2/filter-field';
import {Router} from "@angular/router";
import {SortField} from "../_models/metadata/series-filter";
import {FilterCombination} from "../_models/metadata/v2/filter-combination";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {FilterStatement} from "../_models/metadata/v2/filter-statement";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";

@Injectable({
  providedIn: 'root'
})
export class MetadataService {

  baseUrl = environment.apiUrl;

  private currentThemeSource = new ReplaySubject<SeriesFilterV2>(1);

  private ageRatingTypes: {[key: number]: string} | undefined = undefined;
  private validLanguages: Array<Language> = [];

  constructor(private httpClient: HttpClient, private router: Router) { }

  applyFilter(page: Array<any>, filter: FilterField, comparison: FilterComparison, value: string) {
    const dto: SeriesFilterV2 = {
      statements:  [this.createDefaultFilterStatement(filter, comparison, value + '')],
      combination: FilterCombination.Or,
      limitTo: 0
    };
    //
    // console.log('navigating to: ', this.filterUtilityService.urlFromFilterV2(page.join('/'), dto));
    // this.router.navigateByUrl(this.filterUtilityService.urlFromFilterV2(page.join('/'), dto));

    // Creates a temp name for the filter
    this.httpClient.post<string>(this.baseUrl + 'filter/create-temp', dto, TextResonse).pipe(map(name => {
      dto.name = name;
    }), switchMap((_) => {
      let params: any = {};
      params['filterName'] = dto.name;
      return this.router.navigate(page, {queryParams: params});
    })).subscribe();

  }

  getFilter(filterName: string) {
    return this.httpClient.get<SeriesFilterV2>(this.baseUrl + 'filter?name=' + filterName);
  }

  getAgeRating(ageRating: AgeRating) {
    if (this.ageRatingTypes != undefined && this.ageRatingTypes.hasOwnProperty(ageRating)) {
      return of(this.ageRatingTypes[ageRating]);
    }
    return this.httpClient.get<string>(this.baseUrl + 'series/age-rating?ageRating=' + ageRating, TextResonse).pipe(map(ratingString => {
      if (this.ageRatingTypes === undefined) {
        this.ageRatingTypes = {};
      }

      this.ageRatingTypes[ageRating] = ratingString;
      return this.ageRatingTypes[ageRating];
    }));
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

  getChapterSummary(chapterId: number) {
    return this.httpClient.get<string>(this.baseUrl + 'metadata/chapter-summary?chapterId=' + chapterId, TextResonse);
  }

  createDefaultFilterDto(): SeriesFilterV2 {
    return {
      statements: [] as FilterStatement[],
      combination: FilterCombination.Or,
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
    arr[index].value = filterStmt.value + '';
  }
}
