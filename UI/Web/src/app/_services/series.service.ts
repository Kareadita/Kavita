import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { UtilityService } from '../shared/_services/utility.service';
import { Chapter } from '../_models/chapter';
import { PaginatedResult } from '../_models/pagination';
import { Series } from '../_models/series';
import { RelatedSeries } from '../_models/series-detail/related-series';
import { SeriesDetail } from '../_models/series-detail/series-detail';
import { SeriesGroup } from '../_models/series-group';
import { SeriesMetadata } from '../_models/metadata/series-metadata';
import { Volume } from '../_models/volume';
import { ImageService } from './image.service';
import { TextResonse } from '../_types/text-response';
import { SeriesFilterV2 } from '../_models/metadata/v2/series-filter-v2';
import {UserReview} from "../_single-module/review-card/user-review";
import {Rating} from "../_models/rating";
import {Recommendation} from "../_models/series-detail/recommendation";
import {ExternalSeriesDetail} from "../_models/series-detail/external-series-detail";
import {NextExpectedChapter} from "../_models/series-detail/next-expected-chapter";
import {QueryContext} from "../_models/metadata/v2/query-context";
import {ExternalSeries} from "../_models/series-detail/external-series";
import {ExternalSeriesMatch} from "../_models/series-detail/external-series-match";

@Injectable({
  providedIn: 'root'
})
export class SeriesService {

  baseUrl = environment.apiUrl;
  paginatedResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();
  paginatedSeriesForTagsResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();

  constructor(private httpClient: HttpClient, private imageService: ImageService,
    private utilityService: UtilityService) { }

  getAllSeriesV2(pageNum?: number, itemsPerPage?: number, filter?: SeriesFilterV2, context: QueryContext = QueryContext.None) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = filter || {};

    return this.httpClient.post<PaginatedResult<Series[]>>(this.baseUrl + 'series/all-v2?context=' + context, data, {observe: 'response', params}).pipe(
        map((response: any) => {
          return this.utilityService.createPaginatedResult(response, this.paginatedResults);
        })
    );
  }

  getSeriesForLibraryV2(pageNum?: number, itemsPerPage?: number, filter?: SeriesFilterV2) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = filter || {};

    return this.httpClient.post<PaginatedResult<Series[]>>(this.baseUrl + 'series/v2', data, {observe: 'response', params}).pipe(
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

  delete(seriesId: number) {
    return this.httpClient.delete<string>(this.baseUrl + 'series/' + seriesId, TextResonse).pipe(map(s => s === "true"));
  }

  deleteMultipleSeries(seriesIds: Array<number>) {
    return this.httpClient.post<string>(this.baseUrl + 'series/delete-multiple', {seriesIds}, TextResonse).pipe(map(s => s === "true"));
  }

  updateRating(seriesId: number, userRating: number) {
    return this.httpClient.post(this.baseUrl + 'series/update-rating', {seriesId, userRating});
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

  getRecentlyAdded(pageNum?: number, itemsPerPage?: number, filter?: SeriesFilterV2) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    const data = filter || {};
    return this.httpClient.post<Series[]>(this.baseUrl + 'series/recently-added-v2', data, {observe: 'response', params}).pipe(
      map(response => {
        return this.utilityService.createPaginatedResult(response, new PaginatedResult<Series[]>());
      })
    );
  }

  getRecentlyUpdatedSeries() {
    return this.httpClient.post<SeriesGroup[]>(this.baseUrl + 'series/recently-updated-series', {});
  }

  getWantToRead(pageNum?: number, itemsPerPage?: number, filter?: SeriesFilterV2): Observable<PaginatedResult<Series[]>> {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = filter || {};

    return this.httpClient.post<Series[]>(this.baseUrl + 'want-to-read/v2', data, {observe: 'response', params}).pipe(
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

  getOnDeck(libraryId: number = 0, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilterV2) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = filter || {};

    return this.httpClient.post<Series[]>(this.baseUrl + 'series/on-deck?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map(response => {
        return this.utilityService.createPaginatedResult(response, new PaginatedResult<Series[]>());
    }));
  }


  refreshMetadata(series: Series, force = true, forceColorscape = true) {
    return this.httpClient.post(this.baseUrl + 'series/refresh-metadata', {libraryId: series.libraryId, seriesId: series.id, forceUpdate: force, forceColorscape});
  }

  scan(libraryId: number, seriesId: number, force = false) {
    return this.httpClient.post(this.baseUrl + 'series/scan', {libraryId: libraryId, seriesId: seriesId, forceUpdate: force});
  }

  analyzeFiles(libraryId: number, seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'series/analyze', {libraryId: libraryId, seriesId: seriesId});
  }

  getMetadata(seriesId: number) {
    return this.httpClient.get<SeriesMetadata>(this.baseUrl + 'series/metadata?seriesId=' + seriesId);
  }

  updateMetadata(seriesMetadata: SeriesMetadata) {
    const data = {
      seriesMetadata,
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

  getRecommendationsForSeries(seriesId: number) {
    return this.httpClient.get<Recommendation>(this.baseUrl + 'recommended/recommendations?seriesId=' + seriesId);
  }

  updateRelationships(seriesId: number, adaptations: Array<number>, characters: Array<number>,
    contains: Array<number>, others: Array<number>, prequels: Array<number>,
    sequels: Array<number>, sideStories: Array<number>, spinOffs: Array<number>,
    alternativeSettings: Array<number>, alternativeVersions: Array<number>,
    doujinshis: Array<number>, editions: Array<number>, annuals: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'series/update-related?seriesId=' + seriesId,
    {seriesId, adaptations, characters, sequels, prequels, contains, others, sideStories, spinOffs,
     alternativeSettings, alternativeVersions, doujinshis, editions, annuals});
  }

  getSeriesDetail(seriesId: number) {
    return this.httpClient.get<SeriesDetail>(this.baseUrl + 'series/series-detail?seriesId=' + seriesId);
  }



  deleteReview(seriesId: number) {
    return this.httpClient.delete(this.baseUrl + 'review?seriesId=' + seriesId);
  }
  updateReview(seriesId: number, body: string) {
    return this.httpClient.post<UserReview>(this.baseUrl + 'review', {
      seriesId, body
    });
  }

  getReviews(seriesId: number) {
    return this.httpClient.get<Array<UserReview>>(this.baseUrl + 'review?seriesId=' + seriesId);
  }

  getRatings(seriesId: number) {
    return this.httpClient.get<Array<Rating>>(this.baseUrl + 'rating?seriesId=' + seriesId);
  }
  getOverallRating(seriesId: number) {
    return this.httpClient.get<Rating>(this.baseUrl + 'rating/overall?seriesId=' + seriesId);
  }

  removeFromOnDeck(seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'series/remove-from-on-deck?seriesId=' + seriesId, {});
  }

  getExternalSeriesDetails(aniListId?: number, malId?: number, seriesId?: number) {
    return this.httpClient.get<ExternalSeriesDetail>(this.baseUrl + 'series/external-series-detail?aniListId=' + (aniListId || 0) + '&malId=' + (malId || 0) + '&seriesId=' + (seriesId || 0));
  }

  getNextExpectedChapterDate(seriesId: number) {
    return this.httpClient.get<NextExpectedChapter>(this.baseUrl + 'series/next-expected?seriesId=' + seriesId);
  }

  matchSeries(model: any) {
    return this.httpClient.post<Array<ExternalSeriesMatch>>(this.baseUrl + 'series/match', model);
  }

  updateMatch(seriesId: number, series: ExternalSeriesDetail) {
    return this.httpClient.post<string>(this.baseUrl + 'series/update-match?seriesId=' + seriesId + '&aniListId=' + series.aniListId, {}, TextResonse);
  }

  updateDontMatch(seriesId: number, dontMatch: boolean) {
    return this.httpClient.post<string>(this.baseUrl + `series/dont-match?seriesId=${seriesId}&dontMatch=${dontMatch}`, {}, TextResonse);
  }

}
