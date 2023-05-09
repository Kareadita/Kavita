import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { FilterUtilitiesService } from '../shared/_services/filter-utilities.service';
import { UtilityService } from '../shared/_services/utility.service';
import { Chapter } from '../_models/chapter';
import { ChapterMetadata } from '../_models/metadata/chapter-metadata';
import { CollectionTag } from '../_models/collection-tag';
import { PaginatedResult } from '../_models/pagination';
import { Series } from '../_models/series';
import { RelatedSeries } from '../_models/series-detail/related-series';
import { SeriesDetail } from '../_models/series-detail/series-detail';
import { SeriesFilter } from '../_models/metadata/series-filter';
import { SeriesGroup } from '../_models/series-group';
import { SeriesMetadata } from '../_models/metadata/series-metadata';
import { Volume } from '../_models/volume';
import { ImageService } from './image.service';
import { TextResonse } from '../_types/text-response';
import { FilterGroup } from '../_models/metadata/v2/filter-group';

@Injectable({
  providedIn: 'root'
})
export class SeriesService {

  baseUrl = environment.apiUrl;
  paginatedResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();
  paginatedSeriesForTagsResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();

  constructor(private httpClient: HttpClient, private imageService: ImageService,
    private utilityService: UtilityService, private filterUtilitySerivce: FilterUtilitiesService) { }

  getAllSeries(pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = this.filterUtilitySerivce.createSeriesFilter(filter);

    return this.httpClient.post<PaginatedResult<Series[]>>(this.baseUrl + 'series/all', data, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response, this.paginatedResults);
      })
    );
  }

  getSeriesForLibrary(libraryId: number, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = this.filterUtilitySerivce.createSeriesFilter(filter);

    return this.httpClient.post<PaginatedResult<Series[]>>(this.baseUrl + 'series?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response, this.paginatedResults);
      })
    );
  }

  getSeriesForLibraryV2(pageNum?: number, itemsPerPage?: number, filter?: FilterGroup) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<Series[]>>(this.baseUrl + 'series/v2', filter, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response, this.paginatedResults);
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

  getChapter(chapterId: number) {
    return this.httpClient.get<Chapter>(this.baseUrl + 'series/chapter?chapterId=' + chapterId);
  }

  getChapterMetadata(chapterId: number) {
    return this.httpClient.get<ChapterMetadata>(this.baseUrl + 'series/chapter-metadata?chapterId=' + chapterId);
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

  updateSeries(model: any) {
    return this.httpClient.post(this.baseUrl + 'series/update', model);
  }

  markRead(seriesId: number) {
    return this.httpClient.post<void>(this.baseUrl + 'reader/mark-read', {seriesId});
  }

  markUnread(seriesId: number) {
    return this.httpClient.post<void>(this.baseUrl + 'reader/mark-unread', {seriesId});
  }

  getRecentlyAdded(libraryId: number = 0, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    const data = this.filterUtilitySerivce.createSeriesFilter(filter);
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<Series[]>(this.baseUrl + 'series/recently-added?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map(response => {
        return this.utilityService.createPaginatedResult(response, new PaginatedResult<Series[]>());
      })
    );
  }

  getRecentlyUpdatedSeries() {
    return this.httpClient.post<SeriesGroup[]>(this.baseUrl + 'series/recently-updated-series', {});
  }

  getWantToRead(pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter): Observable<PaginatedResult<Series[]>> {
    const data = this.filterUtilitySerivce.createSeriesFilter(filter);

    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<Series[]>(this.baseUrl + 'want-to-read/', data, {observe: 'response', params}).pipe(
      map(response => {
        return this.utilityService.createPaginatedResult(response, new PaginatedResult<Series[]>());
    }));
  }

  isWantToRead(seriesId: number) {
    return this.httpClient.get<string>(this.baseUrl + 'want-to-read?seriesId=' + seriesId, TextResonse)
    .pipe(map(val => {
      return val === 'true';
    }));
  }

  getOnDeck(libraryId: number = 0, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    const data = this.filterUtilitySerivce.createSeriesFilter(filter);

    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<Series[]>(this.baseUrl + 'series/on-deck?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map(response => {
        return this.utilityService.createPaginatedResult(response, new PaginatedResult<Series[]>());
    }));
  }


  refreshMetadata(series: Series) {
    return this.httpClient.post(this.baseUrl + 'series/refresh-metadata', {libraryId: series.libraryId, seriesId: series.id});
  }

  scan(libraryId: number, seriesId: number, force = false) {
    return this.httpClient.post(this.baseUrl + 'series/scan', {libraryId: libraryId, seriesId: seriesId, forceUpdate: force});
  }

  analyzeFiles(libraryId: number, seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'series/analyze', {libraryId: libraryId, seriesId: seriesId});
  }

  getMetadata(seriesId: number) {
    return this.httpClient.get<SeriesMetadata>(this.baseUrl + 'series/metadata?seriesId=' + seriesId).pipe(map(items => {
      items?.collectionTags.forEach(tag => tag.coverImage = this.imageService.getCollectionCoverImage(tag.id));
      return items;
    }));
  }

  updateMetadata(seriesMetadata: SeriesMetadata, collectionTags: CollectionTag[]) {
    const data = {
      seriesMetadata,
      collectionTags,
    };
    return this.httpClient.post(this.baseUrl + 'series/metadata', data, TextResonse);
  }

  getSeriesForTag(collectionTagId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();

    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'series/series-by-collection?collectionId=' + collectionTagId, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response, this.paginatedSeriesForTagsResults);
      })
    );
  }

  getRelatedForSeries(seriesId: number) {
    return this.httpClient.get<RelatedSeries>(this.baseUrl + 'series/all-related?seriesId=' + seriesId);
  }

  updateRelationships(seriesId: number, adaptations: Array<number>, characters: Array<number>,
    contains: Array<number>, others: Array<number>, prequels: Array<number>,
    sequels: Array<number>, sideStories: Array<number>, spinOffs: Array<number>,
    alternativeSettings: Array<number>, alternativeVersions: Array<number>, doujinshis: Array<number>, editions: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'series/update-related?seriesId=' + seriesId,
    {seriesId, adaptations, characters, sequels, prequels, contains, others, sideStories, spinOffs,
     alternativeSettings, alternativeVersions, doujinshis, editions});
  }

  getSeriesDetail(seriesId: number) {
    return this.httpClient.get<SeriesDetail>(this.baseUrl + 'series/series-detail?seriesId=' + seriesId);
  }
}
