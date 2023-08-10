import {Injectable} from '@angular/core';
import {ActivatedRouteSnapshot} from '@angular/router';
import {Pagination} from 'src/app/_models/pagination';
import {SeriesFilter, SortField, SortOptions} from 'src/app/_models/metadata/series-filter';
import {MetadataService} from "../../_services/metadata.service";
import {SeriesFilterV2} from "../../_models/metadata/v2/series-filter-v2";
import {FilterStatement} from "../../_models/metadata/v2/filter-statement";
import {FilterCombination} from "../../_models/metadata/v2/filter-combination";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";

/**
 * Used to pass state between the filter and the url
 */
export enum FilterQueryParam {
  Format = 'format',
  Genres = 'genres',
  AgeRating = 'ageRating',
  PublicationStatus = 'publicationStatus',
  Tags = 'tags',
  Languages = 'languages',
  CollectionTags = 'collectionTags',
  Libraries = 'libraries',
  Writers = 'writers',
  Artists = 'artists',
  Character = 'character',
  Colorist = 'colorist',
  CoverArtists = 'coverArtists',
  Editor = 'editor',
  Inker = 'inker',
  Letterer = 'letterer',
  Penciller = 'penciller',
  Publisher = 'publisher',
  Translator = 'translators',
  ReadStatus = 'readStatus',
  SortBy = 'sortBy',
  Rating = 'rating',
  Name = 'name',
  /**
   * This is a pagination control
   */
  Page = 'page',
  /**
   * Special case for the UI. Does not trigger filtering
   */
  None = 'none'
}

@Injectable({
  providedIn: 'root'
})
export class FilterUtilitiesService {

  constructor(private metadataService: MetadataService) { }

  /**
   * Updates the window location with a custom url based on filter and pagination objects
   * @param pagination
   * @param filter
   */
  updateUrlFromFilter(pagination: Pagination, filter: SeriesFilter | undefined) {
    const params = '?page=' + pagination.currentPage;

    const url = this.urlFromFilter(window.location.href.split('?')[0] + params, filter);
    window.history.replaceState(window.location.href, '', this.replacePaginationOnUrl(url, pagination));
  }

  /**
   * Patches the page query param in the window location.
   * @param pagination
   */
  updateUrlFromPagination(pagination: Pagination) {
    window.history.replaceState(window.location.href, '', this.replacePaginationOnUrl(window.location.href, pagination));
  }

  private replacePaginationOnUrl(url: string, pagination: Pagination) {
    return url.replace(/page=\d+/i, 'page=' + pagination.currentPage);
  }

  /**
   * Will fetch current page from route if present
   * @param ActivatedRouteSnapshot to fetch page from. Must be from component else may get stale data
   * @param itemsPerPage If you want pagination, pass non-zero number
   * @returns A default pagination object
   */
  pagination(snapshot: ActivatedRouteSnapshot, itemsPerPage: number = 0): Pagination {
    return {currentPage: parseInt(snapshot.queryParamMap.get('page') || '1', 10), itemsPerPage, totalItems: 0, totalPages: 1};
  }


  /**
   * Returns the current url with query params for the filter
   * @param currentUrl Full url, with ?page=1 as a minimum
   * @param filter Filter to build url off
   * @returns current url with query params added
   */
  urlFromFilter(currentUrl: string, filter: SeriesFilter | undefined) {
    if (filter === undefined) return currentUrl;
    let params = '';

    params += this.joinFilter(filter.formats, FilterQueryParam.Format);
    params += this.joinFilter(filter.genres, FilterQueryParam.Genres);
    params += this.joinFilter(filter.ageRating, FilterQueryParam.AgeRating);
    params += this.joinFilter(filter.publicationStatus, FilterQueryParam.PublicationStatus);
    params += this.joinFilter(filter.tags, FilterQueryParam.Tags);
    params += this.joinFilter(filter.languages, FilterQueryParam.Languages);
    params += this.joinFilter(filter.collectionTags, FilterQueryParam.CollectionTags);
    params += this.joinFilter(filter.libraries, FilterQueryParam.Libraries);

    params += this.joinFilter(filter.writers, FilterQueryParam.Writers);
    params += this.joinFilter(filter.artists, FilterQueryParam.Artists);
    params += this.joinFilter(filter.character, FilterQueryParam.Character);
    params += this.joinFilter(filter.colorist, FilterQueryParam.Colorist);
    params += this.joinFilter(filter.coverArtist, FilterQueryParam.CoverArtists);
    params += this.joinFilter(filter.editor, FilterQueryParam.Editor);
    params += this.joinFilter(filter.inker, FilterQueryParam.Inker);
    params += this.joinFilter(filter.letterer, FilterQueryParam.Letterer);
    params += this.joinFilter(filter.penciller, FilterQueryParam.Penciller);
    params += this.joinFilter(filter.publisher, FilterQueryParam.Publisher);
    params += this.joinFilter(filter.translators, FilterQueryParam.Translator);

    // readStatus (we need to do an additonal check as there is a default case)
    if (filter.readStatus && filter.readStatus.inProgress !== true && filter.readStatus.notRead !== true && filter.readStatus.read !== true) {
      params += `&${FilterQueryParam.ReadStatus}=${filter.readStatus.inProgress},${filter.readStatus.notRead},${filter.readStatus.read}`;
    }

    // sortBy (additional check to not save to url if default case)
    if (filter.sortOptions && !(filter.sortOptions.sortField === SortField.SortName && filter.sortOptions.isAscending === true)) {
      params += `&${FilterQueryParam.SortBy}=${filter.sortOptions.sortField},${filter.sortOptions.isAscending}`;
    }

    if (filter.rating > 0) {
      params += `&${FilterQueryParam.Rating}=${filter.rating}`;
    }

    if (filter.seriesNameQuery !== '') {
      params += `&${FilterQueryParam.Name}=${encodeURIComponent(filter.seriesNameQuery)}`;
    }

    return currentUrl + params;
  }

  private joinFilter(filterProp: Array<any>, key: string) {
    let params = '';
    if (filterProp.length > 0) {
      params += `&${key}=${filterProp.join(',')}`;
    }
    return params;
  }

  /**
   * Returns a new instance of a filterSettings that is populated with filter presets from URL
   * @param ActivatedRouteSnapshot to fetch page from. Must be from component else may get stale data
   * @returns The Preset filter and if something was set within
   */
   filterPresetsFromUrl(snapshot: ActivatedRouteSnapshot): [SeriesFilter, boolean] {
    const filter =  this.createSeriesFilter();
    let anyChanged = false;

    const format = snapshot.queryParamMap.get(FilterQueryParam.Format);
    if (format !== undefined && format !== null) {
      filter.formats = [...filter.formats, ...format.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const genres = snapshot.queryParamMap.get(FilterQueryParam.Genres);
    if (genres !== undefined && genres !== null) {
      filter.genres = [...filter.genres, ...genres.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const ageRating = snapshot.queryParamMap.get(FilterQueryParam.AgeRating);
    if (ageRating !== undefined && ageRating !== null) {
      filter.ageRating = [...filter.ageRating, ...ageRating.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const publicationStatus = snapshot.queryParamMap.get(FilterQueryParam.PublicationStatus);
    if (publicationStatus !== undefined && publicationStatus !== null) {
      filter.publicationStatus = [...filter.publicationStatus, ...publicationStatus.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const tags = snapshot.queryParamMap.get(FilterQueryParam.Tags);
    if (tags !== undefined && tags !== null) {
      filter.tags = [...filter.tags, ...tags.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const languages = snapshot.queryParamMap.get(FilterQueryParam.Languages);
    if (languages !== undefined && languages !== null) {
      filter.languages = [...filter.languages, ...languages.split(',')];
      anyChanged = true;
    }

    const writers = snapshot.queryParamMap.get(FilterQueryParam.Writers);
    if (writers !== undefined && writers !== null) {
      filter.writers = [...filter.writers, ...writers.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const artists = snapshot.queryParamMap.get(FilterQueryParam.Artists);
    if (artists !== undefined && artists !== null) {
      filter.artists = [...filter.artists, ...artists.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const character = snapshot.queryParamMap.get(FilterQueryParam.Character);
    if (character !== undefined && character !== null) {
      filter.character = [...filter.character, ...character.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const colorist = snapshot.queryParamMap.get(FilterQueryParam.Colorist);
    if (colorist !== undefined && colorist !== null) {
      filter.colorist = [...filter.colorist, ...colorist.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const coverArtists = snapshot.queryParamMap.get(FilterQueryParam.CoverArtists);
    if (coverArtists !== undefined && coverArtists !== null) {
      filter.coverArtist = [...filter.coverArtist, ...coverArtists.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const editor = snapshot.queryParamMap.get(FilterQueryParam.Editor);
    if (editor !== undefined && editor !== null) {
      filter.editor = [...filter.editor, ...editor.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const inker = snapshot.queryParamMap.get(FilterQueryParam.Inker);
    if (inker !== undefined && inker !== null) {
      filter.inker = [...filter.inker, ...inker.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const letterer = snapshot.queryParamMap.get(FilterQueryParam.Letterer);
    if (letterer !== undefined && letterer !== null) {
      filter.letterer = [...filter.letterer, ...letterer.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const penciller = snapshot.queryParamMap.get(FilterQueryParam.Penciller);
    if (penciller !== undefined && penciller !== null) {
      filter.penciller = [...filter.penciller, ...penciller.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const publisher = snapshot.queryParamMap.get(FilterQueryParam.Publisher);
    if (publisher !== undefined && publisher !== null) {
      filter.publisher = [...filter.publisher, ...publisher.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const translators = snapshot.queryParamMap.get(FilterQueryParam.Translator);
    if (translators !== undefined && translators !== null) {
      filter.translators = [...filter.translators, ...translators.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const libraries = snapshot.queryParamMap.get(FilterQueryParam.Libraries);
    if (libraries !== undefined && libraries !== null) {
      filter.libraries = [...filter.libraries, ...libraries.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const collectionTags = snapshot.queryParamMap.get(FilterQueryParam.CollectionTags);
    if (collectionTags !== undefined && collectionTags !== null) {
      filter.collectionTags = [...filter.collectionTags, ...collectionTags.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    // Rating, seriesName,
    const rating = snapshot.queryParamMap.get(FilterQueryParam.Rating);
    if (rating !== undefined && rating !== null && parseInt(rating, 10) > 0) {
      filter.rating = parseInt(rating, 10);
      anyChanged = true;
    }

    /// Read status is encoded as true,true,true
    const readStatus = snapshot.queryParamMap.get(FilterQueryParam.ReadStatus);
    if (readStatus !== undefined && readStatus !== null) {
      const values = readStatus.split(',').map(i => i === 'true');
      if (values.length === 3) {
        filter.readStatus.inProgress = values[0];
        filter.readStatus.notRead = values[1];
        filter.readStatus.read = values[2];
        anyChanged = true;
      }
    }

    const sortBy = snapshot.queryParamMap.get(FilterQueryParam.SortBy);
    if (sortBy !== undefined && sortBy !== null) {
      const values = sortBy.split(',');
      if (values.length === 1) {
        values.push('true');
      }
      if (values.length === 2) {
        filter.sortOptions = {
          isAscending: values[1] === 'true',
          sortField: Number(values[0])
        }
        anyChanged = true;
      }
    }

    const searchNameQuery = snapshot.queryParamMap.get(FilterQueryParam.Name);
    if (searchNameQuery !== undefined && searchNameQuery !== null && searchNameQuery !== '') {
      filter.seriesNameQuery = decodeURIComponent(searchNameQuery);
      anyChanged = true;
    }


    return [filter, false]; // anyChanged. Testing out if having a filter active but keep drawer closed by default works better
  }

  encodeSeriesFilter(filter: SeriesFilterV2) {
   const encodedStatements = this.encodeFilterStatements(filter.statements);
    const encodedSortOptions = filter.sortOptions ? `sortOptions=${this.encodeSortOptions(filter.sortOptions)}` : '';
    const encodedLimitTo = `limitTo=${filter.limitTo}`;

    return `name=${encodeURIComponent(filter.name || '')}&stmts=${encodedStatements}&${encodedSortOptions}&${encodedLimitTo}&combination=${filter.combination}`;
  }

    encodeSortOptions(sortOptions: SortOptions) {
       return `sortField=${sortOptions.sortField}&isAscending=${sortOptions.isAscending}`;
    }

  encodeFilterStatements(statements: Array<FilterStatement>) {
    return statements.map(statement => {
      const encodedComparison = `comparison=${statement.comparison}`;
      const encodedField = `field=${statement.field}`;
      const encodedValue = `value=${encodeURIComponent(statement.value)}`;

      return `${encodedComparison}&${encodedField}&${encodedValue}`;
    }).join(',');
  }

  filterPresetsFromUrlV2(snapshot: ActivatedRouteSnapshot): SeriesFilterV2 {
    const filter = this.metadataService.createDefaultFilterDto();
    let anyChanged = false;

    const queryParams = snapshot.queryParams;

    if (queryParams.name) {
        filter.name = queryParams.name;
        anyChanged = true;
    }

    if (queryParams.stmts) {
        filter.statements = this.decodeFilterStatements(queryParams.stmts);
        anyChanged = true;
    }

    if (queryParams.sortOptions) {
        const sortOptions = this.decodeSortOptions(queryParams.sortOptions);
        if (sortOptions) {
            filter.sortOptions = sortOptions;
            anyChanged = true;
        }
    }

    if (queryParams.limitTo) {
        filter.limitTo = parseInt(queryParams.limitTo, 10);
        anyChanged = true;
    }

    if (queryParams.combination) {
        filter.combination = queryParams.combination as FilterCombination;
        anyChanged = true;
    }

    // TODO: Implement other query parameters as needed

    return anyChanged ? filter : this.createSeriesV2Filter();
    }

    decodeSortOptions(encodedSortOptions: string): SortOptions | null {
        const parts = encodedSortOptions.split('&');
        const sortFieldPart = parts.find(part => part.startsWith('sortField='));
        const isAscendingPart = parts.find(part => part.startsWith('isAscending='));

        if (sortFieldPart && isAscendingPart) {
            const sortField = parseInt(sortFieldPart.split('=')[1], 10) as SortField;
            const isAscending = isAscendingPart.split('=')[1] === 'true';
            return { sortField, isAscending };
        }

        return null;
    }

    decodeFilterStatements(encodedStatements: string): FilterStatement[] {
        const statementStrings = encodedStatements.split(','); // I don't think this will wrk
        return statementStrings.map(statementString => {
            const parts = statementString.split('&');
            if (parts === null || parts.length <= 3) return null;
            const comparison = parseInt(parts.find(part => part.startsWith('comparison=')).split('=')[1], 10) as FilterComparison;
            const field = parseInt(parts.find(part => part.startsWith('field=')).split('=')[1], 10) as FilterField;
            const value = decodeURIComponent(parts.find(part => part.startsWith('value=')).split('=')[1]);
            return { comparison, field, value };
        });
    }


  createSeriesFilter(filter?: SeriesFilter) {
    if (filter !== undefined) return filter;
    const data: SeriesFilter = {
      formats: [],
      libraries: [],
      genres: [],
      writers: [],
      artists: [],
      penciller: [],
      inker: [],
      colorist: [],
      letterer: [],
      coverArtist: [],
      editor: [],
      publisher: [],
      character: [],
      translators: [],
      collectionTags: [],
      rating: 0,
      readStatus: {
        read: true,
        inProgress: true,
        notRead: true
      },
      sortOptions: null,
      ageRating: [],
      tags: [],
      languages: [],
      publicationStatus: [],
      seriesNameQuery: '',
      releaseYearRange: null
    };

    return data;
  }

  createSeriesV2Filter(): SeriesFilterV2 {
       return {
           combination: FilterCombination.Or,
           statements: [],
           limitTo: 0,
           sortOptions: {
               isAscending: true,
               sortField: SortField.SortName
           },
       };
  }

}
