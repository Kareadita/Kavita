import { Injectable } from '@angular/core';
import { ActivatedRoute, ActivatedRouteSnapshot } from '@angular/router';
import { LibraryType } from 'src/app/_models/library';
import { Pagination } from 'src/app/_models/pagination';
import { SeriesFilter, SortField } from 'src/app/_models/series-filter';
import { SeriesService } from 'src/app/_services/series.service';

@Injectable({
  providedIn: 'root'
})
export class FilterUtilitiesService {

  constructor(private route: ActivatedRoute, private seriesService: SeriesService) { }

  /**
   * Updates the window location with a custom url based on filter and pagination objects
   * @param pagination 
   * @param filter 
   */
  updateUrlFromFilter(pagination: Pagination, filter: SeriesFilter) {
    let params = '?page=' + pagination.currentPage;
  
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
   * @returns A default pagination object
   */
  pagination(): Pagination {
    return {currentPage: parseInt(this.route.snapshot.queryParamMap.get('page') || '1', 10), itemsPerPage: 30, totalItems: 0, totalPages: 1};
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
    

    
    params += this.joinFilter(filter.formats, 'format');
    params += this.joinFilter(filter.genres, 'genres');
    params += this.joinFilter(filter.ageRating, 'ageRating');
    params += this.joinFilter(filter.publicationStatus, 'publicationStatus');
    params += this.joinFilter(filter.tags, 'tags');
    params += this.joinFilter(filter.languages, 'languages');
    params += this.joinFilter(filter.collectionTags, 'collectionTags');
    params += this.joinFilter(filter.libraries, 'libraries');

    params += this.joinFilter(filter.writers, 'writers');
    params += this.joinFilter(filter.artists, 'artists');
    params += this.joinFilter(filter.character, 'character');
    params += this.joinFilter(filter.colorist, 'colorist');
    params += this.joinFilter(filter.coverArtist, 'coverArtists');
    params += this.joinFilter(filter.editor, 'editor');
    params += this.joinFilter(filter.inker, 'inker');
    params += this.joinFilter(filter.letterer, 'letterer');
    params += this.joinFilter(filter.penciller, 'penciller');
    params += this.joinFilter(filter.publisher, 'publisher');
    params += this.joinFilter(filter.translators, 'translators');

    // readStatus (we need to do an additonal check as there is a default case)
    if (filter.readStatus && filter.readStatus.inProgress !== true && filter.readStatus.notRead !== true && filter.readStatus.read !== true) {
      params += '&readStatus=' + `${filter.readStatus.inProgress},${filter.readStatus.notRead},${filter.readStatus.read}`;
    }

    // sortBy (additional check to not save to url if default case)
    if (filter.sortOptions && !(filter.sortOptions.sortField === SortField.SortName && filter.sortOptions.isAscending === true)) {
      params += '&sortBy=' + filter.sortOptions.sortField + ',' + filter.sortOptions.isAscending;
    }

    if (filter.rating > 0) {
      params += '&rating=' + filter.rating;
    }

    if (filter.seriesNameQuery !== '') {
      params += '&name=' + encodeURIComponent(filter.seriesNameQuery);
    }
    
    return currentUrl + params;
  }

  private joinFilter(filterProp: Array<any>, key: string) {
    let params = '';
    if (filterProp.length > 0) {
      params += `&${key}=` + filterProp.join(',');
    }
    return params;
  }

  /**
   * Returns a new instance of a filterSettings that is populated with filter presets from URL
   * @returns The Preset filter and if something was set within
   */
   filterPresetsFromUrl(): [SeriesFilter, boolean] {
    const snapshot = this.route.snapshot;
    const filter =  this.seriesService.createSeriesFilter();
    let anyChanged = false;

    const format = snapshot.queryParamMap.get('format');
    if (format !== undefined && format !== null) {
      filter.formats = [...filter.formats, ...format.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const genres = snapshot.queryParamMap.get('genres');
    if (genres !== undefined && genres !== null) {
      filter.genres = [...filter.genres, ...genres.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const ageRating = snapshot.queryParamMap.get('ageRating');
    if (ageRating !== undefined && ageRating !== null) {
      filter.ageRating = [...filter.ageRating, ...ageRating.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const publicationStatus = snapshot.queryParamMap.get('publicationStatus');
    if (publicationStatus !== undefined && publicationStatus !== null) {
      filter.publicationStatus = [...filter.publicationStatus, ...publicationStatus.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const tags = snapshot.queryParamMap.get('tags');
    if (tags !== undefined && tags !== null) {
      filter.tags = [...filter.tags, ...tags.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const languages = snapshot.queryParamMap.get('languages');
    if (languages !== undefined && languages !== null) {
      filter.languages = [...filter.languages, ...languages.split(',')];
      anyChanged = true;
    }

    const writers = snapshot.queryParamMap.get('writers');
    if (writers !== undefined && writers !== null) {
      filter.writers = [...filter.writers, ...writers.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const artists = snapshot.queryParamMap.get('artists');
    if (artists !== undefined && artists !== null) {
      filter.artists = [...filter.artists, ...artists.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const character = snapshot.queryParamMap.get('character');
    if (character !== undefined && character !== null) {
      filter.character = [...filter.character, ...character.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const colorist = snapshot.queryParamMap.get('colorist');
    if (colorist !== undefined && colorist !== null) {
      filter.colorist = [...filter.colorist, ...colorist.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const coverArtists = snapshot.queryParamMap.get('coverArtists');
    if (coverArtists !== undefined && coverArtists !== null) {
      filter.coverArtist = [...filter.coverArtist, ...coverArtists.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const editor = snapshot.queryParamMap.get('editor');
    if (editor !== undefined && editor !== null) {
      filter.editor = [...filter.editor, ...editor.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const inker = snapshot.queryParamMap.get('inker');
    if (inker !== undefined && inker !== null) {
      filter.inker = [...filter.inker, ...inker.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const letterer = snapshot.queryParamMap.get('letterer');
    if (letterer !== undefined && letterer !== null) {
      filter.letterer = [...filter.letterer, ...letterer.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const penciller = snapshot.queryParamMap.get('penciller');
    if (penciller !== undefined && penciller !== null) {
      filter.penciller = [...filter.penciller, ...penciller.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const publisher = snapshot.queryParamMap.get('publisher');
    if (publisher !== undefined && publisher !== null) {
      filter.publisher = [...filter.publisher, ...publisher.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const translators = snapshot.queryParamMap.get('translators');
    if (translators !== undefined && translators !== null) {
      filter.translators = [...filter.translators, ...translators.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const libraries = snapshot.queryParamMap.get('libraries');
    if (libraries !== undefined && libraries !== null) {
      filter.libraries = [...filter.libraries, ...libraries.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const collectionTags = snapshot.queryParamMap.get('collectionTags');
    if (collectionTags !== undefined && collectionTags !== null) {
      filter.collectionTags = [...filter.collectionTags, ...collectionTags.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    // Rating, seriesName, 
    const rating = snapshot.queryParamMap.get('rating');
    if (rating !== undefined && rating !== null && parseInt(rating, 10) > 0) {
      filter.rating = parseInt(rating, 10);
      anyChanged = true;
    }

    /// Read status is encoded as true,true,true
    const readStatus = snapshot.queryParamMap.get('readStatus');
    if (readStatus !== undefined && readStatus !== null) {
      const values = readStatus.split(',').map(i => i === 'true');
      if (values.length === 3) {
        filter.readStatus.inProgress = values[0];
        filter.readStatus.notRead = values[1];
        filter.readStatus.read = values[2];
        anyChanged = true;
      }
    }

    const sortBy = snapshot.queryParamMap.get('sortBy');
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

    const searchNameQuery = snapshot.queryParamMap.get('name');
    if (searchNameQuery !== undefined && searchNameQuery !== null && searchNameQuery !== '') {
      filter.seriesNameQuery = decodeURIComponent(searchNameQuery);
      anyChanged = true;
    }
    

    return [filter, anyChanged];
  }
}
