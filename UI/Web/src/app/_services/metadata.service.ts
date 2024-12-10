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
import {SortField} from "../_models/metadata/series-filter";
import {FilterCombination} from "../_models/metadata/v2/filter-combination";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {FilterStatement} from "../_models/metadata/v2/filter-statement";
import {SeriesDetailPlus} from "../_models/series-detail/series-detail-plus";
import {LibraryType} from "../_models/library/library";
import {IHasCast} from "../_models/common/i-has-cast";
import {TextResonse} from "../_types/text-response";
import {QueryContext} from "../_models/metadata/v2/query-context";

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

  forceRefreshFromPlus(seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'metadata/force-refresh?seriesId=' + seriesId, {});
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

  getAllGenres(libraries?: Array<number>, context: QueryContext = QueryContext.None) {
    let method = 'metadata/genres'
    if (libraries != undefined && libraries.length > 0) {
      method += '?libraryIds=' + libraries.join(',') + '&context=' + context;
    } else {
      method += '?context=' + context;
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

  getLanguageNameForCode(code: string) {
    return this.httpClient.get<string>(`${this.baseUrl}metadata/language-title?code=${code}`, TextResonse);
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

  updatePerson(entity: IHasCast, persons: Person[], role: PersonRole) {
    switch (role) {
      case PersonRole.Other:
        break;
      case PersonRole.Artist:
        break;
      case PersonRole.CoverArtist:
        entity.coverArtists = persons;
        break;
      case PersonRole.Character:
        entity.characters = persons;
        break;
      case PersonRole.Colorist:
        entity.colorists = persons;
        break;
      case PersonRole.Editor:
        entity.editors = persons;
        break;
      case PersonRole.Inker:
        entity.inkers = persons;
        break;
      case PersonRole.Letterer:
        entity.letterers = persons;
        break;
      case PersonRole.Penciller:
        entity.pencillers = persons;
        break;
      case PersonRole.Publisher:
        entity.publishers = persons;
        break;
      case PersonRole.Imprint:
        entity.imprints = persons;
        break;
      case PersonRole.Team:
        entity.teams = persons;
        break;
      case PersonRole.Location:
        entity.locations = persons;
        break;
      case PersonRole.Writer:
        entity.writers = persons;
        break;
      case PersonRole.Translator:
        entity.translators = persons;
        break;
    }
  }
}
