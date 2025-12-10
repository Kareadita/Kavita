import {HttpClient, HttpParams} from '@angular/common/http';
import {computed, inject, Injectable} from '@angular/core';
import {tap} from 'rxjs/operators';
import {map, Observable, of} from 'rxjs';
import {environment} from 'src/environments/environment';
import {Genre} from '../_models/metadata/genre';
import {AgeRatingDto} from '../_models/metadata/age-rating-dto';
import {Language} from '../_models/metadata/language';
import {PublicationStatusDto} from '../_models/metadata/publication-status-dto';
import {allPeopleRoles, Person, PersonRole} from '../_models/metadata/person';
import {Tag} from '../_models/tag';
import {FilterComparison} from '../_models/metadata/v2/filter-comparison';
import {FilterField} from '../_models/metadata/v2/filter-field';
import {mangaFormatFilters, SortField} from "../_models/metadata/series-filter";
import {FilterCombination} from "../_models/metadata/v2/filter-combination";
import {FilterV2} from "../_models/metadata/v2/filter-v2";
import {FilterStatement} from "../_models/metadata/v2/filter-statement";
import {SeriesDetailPlus} from "../_models/series-detail/series-detail-plus";
import {LibraryType} from "../_models/library/library";
import {IHasCast} from "../_models/common/i-has-cast";
import {TextResonse} from "../_types/text-response";
import {QueryContext} from "../_models/metadata/v2/query-context";
import {AgeRatingPipe} from "../_pipes/age-rating.pipe";
import {MangaFormatPipe} from "../_pipes/manga-format.pipe";
import {translate, TranslocoService} from "@jsverse/transloco";
import {LibraryService} from './library.service';
import {CollectionTagService} from "./collection-tag.service";
import {PaginatedResult} from "../_models/pagination";
import {UtilityService} from "../shared/_services/utility.service";
import {BrowseGenre} from "../_models/metadata/browse/browse-genre";
import {BrowseTag} from "../_models/metadata/browse/browse-tag";
import {ValidFilterEntity} from "../metadata-filter/filter-settings";
import {PersonFilterField} from "../_models/metadata/v2/person-filter-field";
import {PersonRolePipe} from "../_pipes/person-role.pipe";
import {PersonSortField} from "../_models/metadata/v2/person-sort-field";
import {AnnotationsFilterField} from "../_models/metadata/v2/annotations-filter";
import {AccountService} from "./account.service";
import {MemberService} from "./member.service";
import {RgbaColor} from "../book-reader/_models/annotations/highlight-slot";
import {SeriesService} from "./series.service";

@Injectable({
  providedIn: 'root'
})
export class MetadataService {
  private httpClient = inject(HttpClient);


  private readonly translocoService = inject(TranslocoService);
  private readonly libraryService = inject(LibraryService);
  private readonly collectionTagService = inject(CollectionTagService);
  private readonly utilityService = inject(UtilityService);
  private readonly accountService = inject(AccountService);
  private readonly memberService = inject(MemberService)
  private readonly seriesService = inject(SeriesService)

  private readonly highlightSlots = computed(() => {
    return this.accountService.currentUserSignal()?.preferences?.bookReaderHighlightSlots ?? [];
  });

  baseUrl = environment.apiUrl;
  private validLanguages: Array<Language> = [];
  private ageRatingPipe = new AgeRatingPipe();
  private mangaFormatPipe = new MangaFormatPipe();
  private personRolePipe = new PersonRolePipe();

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

  getGenreWithCounts(pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<BrowseGenre[]>>(this.baseUrl + 'metadata/genres-with-counts', {}, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response) as PaginatedResult<BrowseGenre[]>;
      })
    );
  }

  getTagWithCounts(pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<BrowseTag[]>>(this.baseUrl + 'metadata/tags-with-counts', {}, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response) as PaginatedResult<BrowseTag[]>;
      })
    );
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

  createDefaultFilterDto<TFilter extends number, TSort extends number>(entityType: ValidFilterEntity): FilterV2<TFilter, TSort> {
    return {
      statements: [] as FilterStatement<TFilter>[],
      combination: FilterCombination.And,
      limitTo: 0,
      sortOptions: {
        isAscending: true,
        sortField: (entityType === 'series' ? SortField.SortName : PersonSortField.Name) as TSort
      }
    };
  }

  createDefaultFilterStatement(entityType: ValidFilterEntity) {
    switch (entityType) {
      case "annotation":
        const userId = this.accountService.currentUserSignal()?.id;
        if (userId) {
          return this.createFilterStatement(AnnotationsFilterField.Owner, FilterComparison.Equal, `${this.accountService.currentUserSignal()!.id}`);
        }
        return this.createFilterStatement(AnnotationsFilterField.Owner);
      case 'series':
        return this.createFilterStatement(FilterField.SeriesName);
      case 'person':
        return this.createFilterStatement(PersonFilterField.Role, FilterComparison.Contains, `${PersonRole.CoverArtist},${PersonRole.Writer}`);
    }
  }

  createFilterStatement<T extends number = number>(field: T, comparison = FilterComparison.Equal, value = '') {
    return {
      comparison: comparison,
      field: field,
      value: value
    };
  }

  updateFilter(arr: Array<FilterStatement<number>>, index: number, filterStmt: FilterStatement<number>) {
    arr[index].comparison = filterStmt.comparison;
    arr[index].field = filterStmt.field;
    arr[index].value = filterStmt.value ? filterStmt.value + '' : '';
  }

  updatePerson(entity: IHasCast, persons: Person[], role: PersonRole) {
    switch (role) {
      case PersonRole.Other:
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

  /**
   * Used to get the underlying Options (for Metadata Filter Dropdowns)
   * @param filterField
   * @param entityType
   */
  getOptionsForFilterField<T extends number>(filterField: T, entityType: ValidFilterEntity) {
    switch (entityType) {
      case "annotation":
        return this.getAnnotationOptionsForFilterField(filterField as AnnotationsFilterField);
      case 'series':
        return this.getSeriesOptionsForFilterField(filterField as FilterField);
      case 'person':
        return this.getPersonOptionsForFilterField(filterField as PersonFilterField);
    }
  }

  private getAnnotationOptionsForFilterField(field: AnnotationsFilterField): Observable<{value: number, label: string, color?: RgbaColor}[]> {
    switch (field) {
      case AnnotationsFilterField.Owner:
      case AnnotationsFilterField.LikedBy:
        return this.memberService.getMembers(false).pipe(map(members => members.map(member => {
          return {value: member.id, label: member.username};
        })));
      case AnnotationsFilterField.Library:
        return this.libraryService.getLibraries().pipe(map(libs => libs.map(lib => {
          return {value: lib.id, label: lib.name};
        })));
      case AnnotationsFilterField.HighlightSlots:
        return of(this.highlightSlots().map((slot, idx) => {
          return {value: slot.slotNumber, label: translate('highlight-bar.slot-label', {slot: slot.slotNumber + 1}), color: slot.color}; // Slots start at 0
        }));
      case AnnotationsFilterField.Series:
        return this.seriesService.getSeriesWithAnnotations().pipe(map(series => series.map(s => {
          return {value: s.id, label: s.name};
        })));
    }

    return of([]);
  }

  private getPersonOptionsForFilterField(field: PersonFilterField) {
    switch (field) {
      case PersonFilterField.Role:
        return of(allPeopleRoles.map(r => {return {value: r, label: this.personRolePipe.transform(r)}}));
    }
    return of([])
  }

  private getSeriesOptionsForFilterField(field: FilterField) {
    switch (field) {
      case FilterField.PublicationStatus:
        return this.getAllPublicationStatus().pipe(map(pubs => pubs.map(pub => {
          return {value: pub.value, label: pub.title}
        })));
      case FilterField.AgeRating:
        return this.getAllAgeRatings().pipe(map(ratings => ratings.map(rating => {
          return {value: rating.value, label: this.ageRatingPipe.transform(rating.value)}
        })));
      case FilterField.Genres:
        return this.getAllGenres().pipe(map(genres => genres.map(genre => {
          return {value: genre.id, label: genre.title}
        })));
      case FilterField.Languages:
        return this.getAllLanguages().pipe(map(statuses => statuses.map(status => {
          return {value: status.isoCode, label: status.title + ` (${status.isoCode})`}
        })));
      case FilterField.Formats:
        return of(mangaFormatFilters).pipe(map(statuses => statuses.map(status => {
          return {value: status.value, label: this.mangaFormatPipe.transform(status.value)}
        })));
      case FilterField.Libraries:
        return this.libraryService.getLibraries().pipe(map(libs => libs.map(lib => {
          return {value: lib.id, label: lib.name}
        })));
      case FilterField.Tags:
        return this.getAllTags().pipe(map(statuses => statuses.map(status => {
          return {value: status.id, label: status.title}
        })));
      case FilterField.CollectionTags:
        return this.collectionTagService.allCollections().pipe(map(statuses => statuses.map(status => {
          return {value: status.id, label: status.title}
        })));
      case FilterField.Characters: return this.getPersonOptions(PersonRole.Character);
      case FilterField.Colorist: return this.getPersonOptions(PersonRole.Colorist);
      case FilterField.CoverArtist: return this.getPersonOptions(PersonRole.CoverArtist);
      case FilterField.Editor: return this.getPersonOptions(PersonRole.Editor);
      case FilterField.Inker: return this.getPersonOptions(PersonRole.Inker);
      case FilterField.Letterer: return this.getPersonOptions(PersonRole.Letterer);
      case FilterField.Penciller: return this.getPersonOptions(PersonRole.Penciller);
      case FilterField.Publisher: return this.getPersonOptions(PersonRole.Publisher);
      case FilterField.Imprint: return this.getPersonOptions(PersonRole.Imprint);
      case FilterField.Team: return this.getPersonOptions(PersonRole.Team);
      case FilterField.Location: return this.getPersonOptions(PersonRole.Location);
      case FilterField.Translators: return this.getPersonOptions(PersonRole.Translator);
      case FilterField.Writers: return this.getPersonOptions(PersonRole.Writer);
    }

    return of([]);
  }

  private getPersonOptions(role: PersonRole) {
    return this.getAllPeopleByRole(role).pipe(map(people => people.map(person => {
      return {value: person.id, label: person.name}
    })));
  }
}
