import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ChapterInfo } from '../manga-reader/_models/chapter-info';
import { UtilityService } from '../shared/_services/utility.service';
import { Chapter } from '../_models/chapter';
import { HourEstimateRange } from '../_models/hour-estimate-range';
import { MangaFormat } from '../_models/manga-format';
import { BookmarkInfo } from '../_models/manga-reader/bookmark-info';
import { PageBookmark } from '../_models/page-bookmark';
import { ProgressBookmark } from '../_models/progress-bookmark';

export const CHAPTER_ID_DOESNT_EXIST = -1;
export const CHAPTER_ID_NOT_FETCHED = -2;

@Injectable({
  providedIn: 'root'
})
export class ReaderService {

  baseUrl = environment.apiUrl;

  // Override background color for reader and restore it onDestroy
  private originalBodyColor!: string;

  constructor(private httpClient: HttpClient, private utilityService: UtilityService) { }

  getNavigationArray(libraryId: number, seriesId: number, chapterId: number, format: MangaFormat) {
    if (format === undefined) format = MangaFormat.ARCHIVE;

    if (format === MangaFormat.EPUB) {
      return ['library', libraryId, 'series', seriesId, 'book', chapterId];
    } else if (format === MangaFormat.PDF) {
      return ['library', libraryId, 'series', seriesId, 'pdf', chapterId];
    } else {
      return ['library', libraryId, 'series', seriesId, 'manga', chapterId];
    }
  }

  downloadPdf(chapterId: number) {
    return this.baseUrl + 'reader/pdf?chapterId=' + chapterId;
  }

  bookmark(seriesId: number, volumeId: number, chapterId: number, page: number) {
    return this.httpClient.post(this.baseUrl + 'reader/bookmark', {seriesId, volumeId, chapterId, page});
  }

  unbookmark(seriesId: number, volumeId: number, chapterId: number, page: number) {
    return this.httpClient.post(this.baseUrl + 'reader/unbookmark', {seriesId, volumeId, chapterId, page});
  }

  getAllBookmarks() {
    return this.httpClient.get<PageBookmark[]>(this.baseUrl + 'reader/get-all-bookmarks');
  }

  getBookmarks(chapterId: number) {
    return this.httpClient.get<PageBookmark[]>(this.baseUrl + 'reader/get-bookmarks?chapterId=' + chapterId);
  }

  getBookmarksForVolume(volumeId: number) {
    return this.httpClient.get<PageBookmark[]>(this.baseUrl + 'reader/get-volume-bookmarks?volumeId=' + volumeId);
  }

  getBookmarksForSeries(seriesId: number) {
    return this.httpClient.get<PageBookmark[]>(this.baseUrl + 'reader/get-series-bookmarks?seriesId=' + seriesId);
  }

  clearBookmarks(seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'reader/remove-bookmarks', {seriesId});
  }

  /**
   * Used exclusively for reading multiple bookmarks from a series
   * @param seriesId
   */
  getBookmarkInfo(seriesId: number) {
    return this.httpClient.get<BookmarkInfo>(this.baseUrl + 'reader/bookmark-info?seriesId=' + seriesId);
  }

  getProgress(chapterId: number) {
    return this.httpClient.get<ProgressBookmark>(this.baseUrl + 'reader/get-progress?chapterId=' + chapterId);
  }

  getPageUrl(chapterId: number, page: number) {
    return this.baseUrl + 'reader/image?chapterId=' + chapterId + '&page=' + page;
  }

  getBookmarkPageUrl(seriesId: number, apiKey: string, page: number) {
    return this.baseUrl + 'reader/bookmark-image?seriesId=' + seriesId + '&page=' + page + '&apiKey=' + encodeURIComponent(apiKey);
  }

  getChapterInfo(chapterId: number) {
    return this.httpClient.get<ChapterInfo>(this.baseUrl + 'reader/chapter-info?chapterId=' + chapterId);
  }

  saveProgress(seriesId: number, volumeId: number, chapterId: number, page: number, bookScrollId: string | null = null) {
    return this.httpClient.post(this.baseUrl + 'reader/progress', {seriesId, volumeId, chapterId, pageNum: page, bookScrollId});
  }

  markVolumeRead(seriesId: number, volumeId: number) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-volume-read', {seriesId, volumeId});
  }

  markMultipleRead(seriesId: number, volumeIds: Array<number>,  chapterIds?: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-multiple-read', {seriesId, volumeIds, chapterIds});
  }

  markMultipleUnread(seriesId: number, volumeIds: Array<number>,  chapterIds?: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-multiple-unread', {seriesId, volumeIds, chapterIds});
  }

  markMultipleSeriesRead(seriesIds: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-multiple-series-read', {seriesIds});
  }

  markMultipleSeriesUnread(seriesIds: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-multiple-series-unread', {seriesIds});
  }

  markVolumeUnread(seriesId: number, volumeId: number) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-volume-unread', {seriesId, volumeId});
  }


  getNextChapter(seriesId: number, volumeId: number, currentChapterId: number, readingListId: number = -1) {
    if (readingListId > 0) {
      return this.httpClient.get<number>(this.baseUrl + 'readinglist/next-chapter?seriesId=' + seriesId + '&currentChapterId=' + currentChapterId + '&readingListId=' + readingListId);
    }
    return this.httpClient.get<number>(this.baseUrl + 'reader/next-chapter?seriesId=' + seriesId + '&volumeId=' + volumeId + '&currentChapterId=' + currentChapterId);
  }

  getPrevChapter(seriesId: number, volumeId: number, currentChapterId: number, readingListId: number = -1) {
    if (readingListId > 0) {
      return this.httpClient.get<number>(this.baseUrl + 'readinglist/prev-chapter?seriesId=' + seriesId + '&currentChapterId=' + currentChapterId + '&readingListId=' + readingListId);
    }
    return this.httpClient.get<number>(this.baseUrl + 'reader/prev-chapter?seriesId=' + seriesId + '&volumeId=' + volumeId + '&currentChapterId=' + currentChapterId);
  }

  hasSeriesProgress(seriesId: number) {
    return this.httpClient.get<boolean>(this.baseUrl + 'reader/has-progress?seriesId=' + seriesId);
  }

  getCurrentChapter(seriesId: number) {
    return this.httpClient.get<Chapter>(this.baseUrl + 'reader/continue-point?seriesId=' + seriesId);
  }

  getTimeLeft(seriesId: number) {
    return this.httpClient.get<HourEstimateRange>(this.baseUrl + 'reader/time-left?seriesId=' + seriesId);
  }

  /**
   * Captures current body color and forces background color to be black. Call @see resetOverrideStyles() on destroy of component to revert changes
   */
  setOverrideStyles(backgroundColor: string = 'black') {
    const bodyNode = document.querySelector('body');
    if (bodyNode !== undefined && bodyNode !== null) {
      this.originalBodyColor = bodyNode.style.background;
      bodyNode.setAttribute('style', 'background-color: ' + backgroundColor + ' !important');
    }
  }

  resetOverrideStyles() {
    const bodyNode = document.querySelector('body');
    if (bodyNode !== undefined && bodyNode !== null && this.originalBodyColor !== undefined) {
      bodyNode.style.background = this.originalBodyColor;
    }
  }

  /**
   * Parses out the page number from a Image src url
   * @param imageSrc Src attribute of Image
   * @returns
   */
  imageUrlToPageNum(imageSrc: string) {
    if (imageSrc === undefined || imageSrc === '') { return -1; }
    return parseInt(imageSrc.split('&page=')[1], 10);
  }

  getNextChapterUrl(url: string, nextChapterId: number, incognitoMode: boolean = false, readingListMode: boolean = false, readingListId: number = -1) {
    const lastSlashIndex = url.lastIndexOf('/');
    let newRoute = url.substring(0, lastSlashIndex + 1) + nextChapterId + '';
    newRoute += this.getQueryParams(incognitoMode, readingListMode, readingListId);
    return newRoute;
  }


  getQueryParamsObject(incognitoMode: boolean = false, readingListMode: boolean = false, readingListId: number = -1) {
    let params: {[key: string]: any} = {};
    if (incognitoMode) {
      params['incognitoMode'] = true;
    }
    if (readingListMode) {
      params['readingListId'] = readingListId;
    }
    return params;
  }

  getQueryParams(incognitoMode: boolean = false, readingListMode: boolean = false, readingListId: number = -1) {
    let params = '';
    if (incognitoMode) {
      params += '?incognitoMode=true';
    }
    if (readingListMode) {
      if (params.indexOf('?') > 0) {
        params += '&readingListId=' + readingListId;
      } else {
        params += '?readingListId=' + readingListId;
      }
    }
    return params;
  }

  enterFullscreen(el: Element, callback?: VoidFunction) {
    if (!document.fullscreenElement) {
      if (el.requestFullscreen) {
        el.requestFullscreen().then(() => {
          if (callback) {
            callback();
          }
        });
      }
    }
  }

  exitFullscreen(callback?: VoidFunction) {
    if (document.exitFullscreen && this.checkFullscreenMode()) {
      document.exitFullscreen().then(() => {
        if (callback) {
          callback();
        }
      });
    }
  }

  /**
   *
   * @returns If document is in fullscreen mode
   */
  checkFullscreenMode() {
    return document.fullscreenElement != null;
  }
}
