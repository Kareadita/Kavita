import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { UtilityService } from '../shared/_services/utility.service';
import { Person } from '../_models/metadata/person';
import { PaginatedResult } from '../_models/pagination';
import { ReadingList, ReadingListItem } from '../_models/reading-list';
import { CblImportSummary } from '../_models/reading-list/cbl/cbl-import-summary';
import { TextResonse } from '../_types/text-response';
import {Action, ActionItem} from './action-factory.service';

@Injectable({
  providedIn: 'root'
})
export class ReadingListService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private utilityService: UtilityService) { }

  getReadingList(readingListId: number) {
    return this.httpClient.get<ReadingList>(this.baseUrl + 'readinglist?readingListId=' + readingListId);
  }

  getReadingLists(includePromoted: boolean = true, sortByLastModified: boolean = false, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<ReadingList[]>>(this.baseUrl + 'readinglist/lists?includePromoted=' + includePromoted
    + '&sortByLastModified=' + sortByLastModified, {}, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response, new PaginatedResult<ReadingList[]>());
      })
    );
  }

  getReadingListsForSeries(seriesId: number) {
    return this.httpClient.get<ReadingList[]>(this.baseUrl + 'readinglist/lists-for-series?seriesId=' + seriesId);
  }

  getReadingListsForChapter(chapterId: number) {
    return this.httpClient.get<ReadingList[]>(this.baseUrl + 'readinglist/lists-for-chapter?chapterId=' + chapterId);
  }

  getListItems(readingListId: number) {
    return this.httpClient.get<ReadingListItem[]>(this.baseUrl + 'readinglist/items?readingListId=' + readingListId);
  }

  createList(title: string) {
    return this.httpClient.post<ReadingList>(this.baseUrl + 'readinglist/create', {title});
  }

  update(model: {readingListId: number, title?: string, summary?: string, promoted: boolean}) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update', model, TextResonse);
  }

  updateByMultiple(readingListId: number, seriesId: number, volumeIds: Array<number>,  chapterIds?: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-multiple', {readingListId, seriesId, volumeIds, chapterIds}, TextResonse);
  }

  updateByMultipleSeries(readingListId: number, seriesIds: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-multiple-series', {readingListId, seriesIds}, TextResonse);
  }

  updateBySeries(readingListId: number, seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-series', {readingListId, seriesId}, TextResonse);
  }

  updateByVolume(readingListId: number, seriesId: number, volumeId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-volume', {readingListId, seriesId, volumeId}, TextResonse);
  }

  updateByChapter(readingListId: number, seriesId: number, chapterId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-chapter', {readingListId, seriesId, chapterId}, TextResonse);
  }

  delete(readingListId: number) {
    return this.httpClient.delete(this.baseUrl + 'readinglist?readingListId=' + readingListId, TextResonse);
  }

  updatePosition(readingListId: number, readingListItemId: number, fromPosition: number, toPosition: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-position', {readingListId, readingListItemId, fromPosition, toPosition}, TextResonse);
  }

  deleteItem(readingListId: number, readingListItemId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/delete-item', {readingListId, readingListItemId}, TextResonse);
  }

  removeRead(readingListId: number) {
    return this.httpClient.post<string>(this.baseUrl + 'readinglist/remove-read?readingListId=' + readingListId, {}, TextResonse);
  }

  actionListFilter(action: ActionItem<ReadingList>, readingList: ReadingList, canPromote: boolean) {

    const isPromotionAction = action.action == Action.Promote || action.action == Action.UnPromote;

    if (isPromotionAction) return canPromote;
    return true;

    // if (readingList?.promoted && !isAdmin) return false;
    // return true;
  }

  nameExists(name: string) {
    return this.httpClient.get<boolean>(this.baseUrl + 'readinglist/name-exists?name=' + name);
  }

  validateCbl(form: FormData, dryRun: boolean, useComicVineMatching: boolean) {
    return this.httpClient.post<CblImportSummary>(this.baseUrl + `cbl/validate?dryRun=${dryRun}&useComicVineMatching=${useComicVineMatching}`, form);
  }

  importCbl(form: FormData, dryRun: boolean, useComicVineMatching: boolean) {
    return this.httpClient.post<CblImportSummary>(this.baseUrl + `cbl/import?dryRun=${dryRun}&useComicVineMatching=${useComicVineMatching}`, form);
  }

  getCharacters(readingListId: number) {
    return this.httpClient.get<Array<Person>>(this.baseUrl + 'readinglist/characters?readingListId=' + readingListId);
  }

  promoteMultipleReadingLists(listIds: Array<number>, promoted: boolean) {
    return this.httpClient.post(this.baseUrl + 'readinglist/promote-multiple', {readingListIds: listIds, promoted}, TextResonse);
  }

  deleteMultipleReadingLists(listIds: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'readinglist/delete-multiple', {readingListIds: listIds}, TextResonse);
  }


}
