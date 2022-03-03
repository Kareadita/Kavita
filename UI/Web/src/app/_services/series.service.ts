import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Chapter } from '../_models/chapter';
import { CollectionTag } from '../_models/collection-tag';
import { PaginatedResult } from '../_models/pagination';
import { RecentlyAddedItem } from '../_models/recently-added-item';
import { Series } from '../_models/series';
import { SeriesDetail } from '../_models/series-detail/series-detail';
import { SeriesFilter } from '../_models/series-filter';
import { SeriesGroup } from '../_models/series-group';
import { SeriesMetadata } from '../_models/series-metadata';
import { Volume } from '../_models/volume';
import { ImageService } from './image.service';

@Injectable({
  providedIn: 'root'
})
export class SeriesService {

  baseUrl = environment.apiUrl;
  paginatedResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();
  paginatedSeriesForTagsResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();

  constructor(private httpClient: HttpClient, private imageService: ImageService) { }

  _cachePaginatedResults(response: any, paginatedVariable: PaginatedResult<any[]>) {
    if (response.body === null) {
      paginatedVariable.result = [];
    } else {
      paginatedVariable.result = response.body;
    }

    const pageHeader = response.headers.get('Pagination');
    if (pageHeader !== null) {
      paginatedVariable.pagination = JSON.parse(pageHeader);
    }

    return paginatedVariable;
  }

  getAllSeries(pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    let params = new HttpParams();
    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = this.createSeriesFilter(filter);

    return this.httpClient.post<PaginatedResult<Series[]>>(this.baseUrl + 'series/all', data, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this._cachePaginatedResults(response, this.paginatedResults);
      })
    );
  }

  getSeriesForLibrary(libraryId: number, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    let params = new HttpParams();
    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = this.createSeriesFilter(filter);

    return this.httpClient.post<PaginatedResult<Series[]>>(this.baseUrl + 'series?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this._cachePaginatedResults(response, this.paginatedResults);
      })
    );
  }

  getAllSeriesByIds(seriesIds: Array<number>) {
    return this.httpClient.post<Series[]>(this.baseUrl + 'series/series-by-ids', {seriesIds: seriesIds});
  }

  getSeries(seriesId: number) {
    return this.httpClient.get<Series>(this.baseUrl + 'series/' + seriesId);
  }

  getVolumes(seriesId: number) {
    return this.httpClient.get<Volume[]>(this.baseUrl + 'series/volumes?seriesId=' + seriesId);
  }

  getVolume(volumeId: number) {
    return this.httpClient.get<Volume>(this.baseUrl + 'series/volume?volumeId=' + volumeId);
  }

  getChapter(chapterId: number) {
    return this.httpClient.get<Chapter>(this.baseUrl + 'series/chapter?chapterId=' + chapterId);
  }

  getData(id: number) {
    return of(id);
  }

  delete(seriesId: number) {
    return this.httpClient.delete<boolean>(this.baseUrl + 'series/' + seriesId);
  }

  deleteMultipleSeries(seriesIds: Array<number>) {
    return this.httpClient.post<boolean>(this.baseUrl + 'series/delete-multiple', {seriesIds});
  }

  updateRating(seriesId: number, userRating: number, userReview: string) {
    return this.httpClient.post(this.baseUrl + 'series/update-rating', {seriesId, userRating, userReview});
  }

  updateSeries(model: any, unlockName = false, unlockSortName = false, unlockLocalizedName = false) {
    const data = {...model, unlockName, unlockSortName, unlockLocalizedName};
    return this.httpClient.post(this.baseUrl + 'series/update', data);
  }

  markRead(seriesId: number) {
    return this.httpClient.post<void>(this.baseUrl + 'reader/mark-read', {seriesId});
  }

  markUnread(seriesId: number) {
    return this.httpClient.post<void>(this.baseUrl + 'reader/mark-unread', {seriesId});
  }

  getRecentlyAdded(libraryId: number = 0, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    const data = this.createSeriesFilter(filter);
    let params = new HttpParams();
    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<Series[]>(this.baseUrl + 'series/recently-added?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map(response => {
        return this._cachePaginatedResults(response, new PaginatedResult<Series[]>());
      })
    );
  }

  getRecentlyUpdatedSeries() {
    return this.httpClient.post<SeriesGroup[]>(this.baseUrl + 'series/recently-updated-series', {});
  }
  getRecentlyAddedChapters() {
    return this.httpClient.post<RecentlyAddedItem[]>(this.baseUrl + 'series/recently-added-chapters', {});
  }

  getOnDeck(libraryId: number = 0, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    const data = this.createSeriesFilter(filter);

    let params = new HttpParams();
    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<Series[]>(this.baseUrl + 'series/on-deck?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map(response => {
        return this._cachePaginatedResults(response, new PaginatedResult<Series[]>());
    }));
  }


  refreshMetadata(series: Series) {
    return this.httpClient.post(this.baseUrl + 'series/refresh-metadata', {libraryId: series.libraryId, seriesId: series.id});
  }

  scan(libraryId: number, seriesId: number) {
    // TODO: Pipe and put a toaster up: this.toastr.info('Scan queued for ' + series.name);
    return this.httpClient.post(this.baseUrl + 'series/scan', {libraryId: libraryId, seriesId: seriesId});
  }

  getMetadata(seriesId: number) {
    return this.httpClient.get<SeriesMetadata>(this.baseUrl + 'series/metadata?seriesId=' + seriesId).pipe(map(items => {
      items?.collectionTags.forEach(tag => tag.coverImage = this.imageService.getCollectionCoverImage(tag.id));
      return items;
    }));
  }

  updateMetadata(seriesMetadata: SeriesMetadata, collectionTags: CollectionTag[],
    genresLocked: boolean = false, tagsLocked: boolean = false, writersLocked: boolean = false,
    coverArtistsLocked: boolean = false, publishersLocked: boolean = false, charactersLocked: boolean = false, 
    pencillersLocked: boolean = false, inkersLocked: boolean = false, coloristsLocked: boolean = false, letterersLocked: boolean = false, 
    editorsLocked: boolean = false, translatorsLocked: boolean = false, ageRatingLocked: boolean = false, languageLocked: boolean = false, 
    publicationStatusLocked: boolean = false) {
    const data = {
      seriesMetadata,
      collectionTags,
      genresLocked,
      tagsLocked,
      writersLocked,
      coverArtistsLocked,
      publishersLocked,
      charactersLocked,
      pencillersLocked,
      inkersLocked,
      coloristsLocked,
      letterersLocked,
      editorsLocked,
      translatorsLocked,
      ageRatingLocked,
      languageLocked,
      publicationStatusLocked,
    };
    return this.httpClient.post(this.baseUrl + 'series/metadata', data, {responseType: 'text' as 'json'});
  }

  getSeriesForTag(collectionTagId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();

    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'series/series-by-collection?collectionId=' + collectionTagId, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this._cachePaginatedResults(response, this.paginatedSeriesForTagsResults);
      })
    );
  }

  getSeriesDetail(seriesId: number) {
    return this.httpClient.get<SeriesDetail>(this.baseUrl + 'series/series-detail?seriesId=' + seriesId);
  }

  _addPaginationIfExists(params: HttpParams, pageNum?: number, itemsPerPage?: number) {
    if (pageNum !== null && pageNum !== undefined && itemsPerPage !== null && itemsPerPage !== undefined) {
      params = params.append('pageNumber', pageNum + '');
      params = params.append('pageSize', itemsPerPage + '');
    }
    return params;
  }

  createSeriesFilter(filter?: SeriesFilter) {
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
    };

    if (filter === undefined) return data;

    return filter;
  }
}
