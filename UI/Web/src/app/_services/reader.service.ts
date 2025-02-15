import { HttpClient, HttpParams } from '@angular/common/http';
import {DestroyRef, Inject, inject, Injectable} from '@angular/core';
import {DOCUMENT, Location} from '@angular/common';
import { Router } from '@angular/router';
import { environment } from 'src/environments/environment';
import { ChapterInfo } from '../manga-reader/_models/chapter-info';
import { Chapter } from '../_models/chapter';
import { HourEstimateRange } from '../_models/series-detail/hour-estimate-range';
import { MangaFormat } from '../_models/manga-format';
import { BookmarkInfo } from '../_models/manga-reader/bookmark-info';
import { PageBookmark } from '../_models/readers/page-bookmark';
import { ProgressBookmark } from '../_models/readers/progress-bookmark';
import { FileDimension } from '../manga-reader/_models/file-dimension';
import screenfull from 'screenfull';
import { TextResonse } from '../_types/text-response';
import { AccountService } from './account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {PersonalToC} from "../_models/readers/personal-toc";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import NoSleep from 'nosleep.js';
import {FullProgress} from "../_models/readers/full-progress";
import {Volume} from "../_models/volume";
import {UtilityService} from "../shared/_services/utility.service";
import {translate} from "@jsverse/transloco";
import {ToastrService} from "ngx-toastr";


export const CHAPTER_ID_DOESNT_EXIST = -1;
export const CHAPTER_ID_NOT_FETCHED = -2;

@Injectable({
  providedIn: 'root'
})
export class ReaderService {

  private readonly destroyRef = inject(DestroyRef);
  private readonly utilityService = inject(UtilityService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);

  baseUrl = environment.apiUrl;
  encodedKey: string = '';

  // Override background color for reader and restore it onDestroy
  private originalBodyColor!: string;

  private noSleep = new NoSleep();

  constructor(private httpClient: HttpClient, @Inject(DOCUMENT) private document: Document) {
      this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
        if (user) {
          this.encodedKey = encodeURIComponent(user.apiKey);
        }
      });
  }

  enableWakeLock(element?: Element | Document) {
    // Enable wake lock.
    // (must be wrapped in a user input event handler e.g. a mouse or touch handler)

    if (!element) element = this.document;

    const enableNoSleepHandler = () => {
      element!.removeEventListener('click', enableNoSleepHandler, false);
      element!.removeEventListener('touchmove', enableNoSleepHandler, false);
      element!.removeEventListener('mousemove', enableNoSleepHandler, false);
      this.noSleep!.enable();
    };

    // Enable wake lock.
    // (must be wrapped in a user input event handler e.g. a mouse or touch handler)
    element.addEventListener('click', enableNoSleepHandler, false);
    element.addEventListener('touchmove', enableNoSleepHandler, false);
    element.addEventListener('mousemove', enableNoSleepHandler, false);
  }

  disableWakeLock() {
    this.noSleep.disable();
  }


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
    return `${this.baseUrl}reader/pdf?chapterId=${chapterId}&apiKey=${this.encodedKey}`;
  }

  bookmark(seriesId: number, volumeId: number, chapterId: number, page: number) {
    return this.httpClient.post(this.baseUrl + 'reader/bookmark', {seriesId, volumeId, chapterId, page});
  }

  unbookmark(seriesId: number, volumeId: number, chapterId: number, page: number) {
    return this.httpClient.post(this.baseUrl + 'reader/unbookmark', {seriesId, volumeId, chapterId, page});
  }

  getAllBookmarks(filter: SeriesFilterV2 | undefined) {
    return this.httpClient.post<PageBookmark[]>(this.baseUrl + 'reader/all-bookmarks', filter);
  }


  getBookmarks(chapterId: number) {
    return this.httpClient.get<PageBookmark[]>(this.baseUrl + 'reader/chapter-bookmarks?chapterId=' + chapterId);
  }

  getBookmarksForVolume(volumeId: number) {
    return this.httpClient.get<PageBookmark[]>(this.baseUrl + 'reader/volume-bookmarks?volumeId=' + volumeId);
  }

  getBookmarksForSeries(seriesId: number) {
    return this.httpClient.get<PageBookmark[]>(this.baseUrl + 'reader/series-bookmarks?seriesId=' + seriesId);
  }

  clearBookmarks(seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'reader/remove-bookmarks', {seriesId}, TextResonse);
  }
  clearMultipleBookmarks(seriesIds: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'reader/bulk-remove-bookmarks', {seriesIds}, TextResonse);
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
    return `${this.baseUrl}reader/image?chapterId=${chapterId}&apiKey=${this.encodedKey}&page=${page}`;
  }

  getThumbnailUrl(chapterId: number, page: number) {
    return `${this.baseUrl}reader/thumbnail?chapterId=${chapterId}&apiKey=${this.encodedKey}&page=${page}`;
  }

  getBookmarkPageUrl(seriesId: number, apiKey: string, page: number) {
    return this.baseUrl + 'reader/bookmark-image?seriesId=' + seriesId + '&page=' + page + '&apiKey=' + encodeURIComponent(apiKey);
  }

  getChapterInfo(chapterId: number, includeDimensions = false) {
    return this.httpClient.get<ChapterInfo>(this.baseUrl + 'reader/chapter-info?chapterId=' + chapterId + '&includeDimensions=' + includeDimensions);
  }

  getFileDimensions(chapterId: number) {
    return this.httpClient.get<Array<FileDimension>>(this.baseUrl + 'reader/file-dimensions?chapterId=' + chapterId);
  }

  saveProgress(libraryId: number, seriesId: number, volumeId: number, chapterId: number, page: number, bookScrollId: string | null = null) {
    return this.httpClient.post(this.baseUrl + 'reader/progress', {libraryId, seriesId, volumeId, chapterId, pageNum: page, bookScrollId});
  }

  getAllProgressForChapter(chapterId: number) {
    return this.httpClient.get<Array<FullProgress>>(this.baseUrl + 'reader/all-chapter-progress?chapterId=' + chapterId);
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
    const params = new URLSearchParams(new URL(imageSrc).search);
    return parseInt(params.get('page') || '-1', 10);
  }

  imageUrlToChapterId(imageSrc: string) {
    if (imageSrc === undefined || imageSrc === '') { return -1; }
    const params = new URLSearchParams(new URL(imageSrc).search);
    return parseInt(params.get('chapterId') || '-1', 10);
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

  toggleFullscreen(el: Element, callback?: VoidFunction) {

    if (screenfull.isEnabled) {
      screenfull.toggle();
    }
  }

  /**
   *
   * @returns If document is in fullscreen mode
   */
  checkFullscreenMode() {
    return document.fullscreenElement != null;
  }

  /**
   * Closes the reader and causes a redirection
   */
  closeReader(readingListMode: boolean = false, readingListId: number = 0) {
    if (readingListMode) {
      this.router.navigateByUrl('lists/' + readingListId);
    } else {
      // TODO: back doesn't always work, it might be nice to check the pattern of the url and see if we can be smart before just going back
      this.location.back();
    }
  }

  removePersonalToc(chapterId: number, pageNumber: number, title: string) {
    return this.httpClient.delete(this.baseUrl + `reader/ptoc?chapterId=${chapterId}&pageNum=${pageNumber}&title=${encodeURIComponent(title)}`);
  }

  getPersonalToC(chapterId: number) {
    return this.httpClient.get<Array<PersonalToC>>(this.baseUrl + 'reader/ptoc?chapterId=' + chapterId);
  }

  createPersonalToC(libraryId: number, seriesId: number, volumeId: number, chapterId: number, pageNumber: number, title: string, bookScrollId: string | null) {
    return this.httpClient.post(this.baseUrl + 'reader/create-ptoc', {libraryId, seriesId, volumeId, chapterId, pageNumber, title, bookScrollId});
  }

  getElementFromXPath(path: string) {
    const node = document.evaluate(path, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;
    if (node?.nodeType === Node.ELEMENT_NODE) {
      return node as Element;
    }
    return null;
  }

  /**
   *
   * @param element
   * @param pureXPath Will ignore shortcuts like id('')
   */
  getXPathTo(element: any, pureXPath = false): string {
    if (element === null) return '';
    if (!pureXPath) {
      if (element.id !== '') { return 'id("' + element.id + '")'; }
      if (element === document.body) { return element.tagName; }
    }


    let ix = 0;
    const siblings = element.parentNode?.childNodes || [];
    for (let sibling of siblings) {
      if (sibling === element) {
        return this.getXPathTo(element.parentNode) + '/' + element.tagName + '[' + (ix + 1) + ']';
      }
      if (sibling.nodeType === 1 && sibling.tagName === element.tagName) {
        ix++;
      }

    }
    return '';
  }

  readVolume(libraryId: number, seriesId: number, volume: Volume, incognitoMode: boolean = false) {
    if (volume.pagesRead < volume.pages && volume.pagesRead > 0) {
      // Find the continue point chapter and load it
      const unreadChapters = volume.chapters.filter(item => item.pagesRead < item.pages);
      if (unreadChapters.length > 0) {
        this.readChapter(libraryId, seriesId, unreadChapters[0], incognitoMode);
        return;
      }
      this.readChapter(libraryId, seriesId, volume.chapters[0], incognitoMode);
      return;
    }

    // Sort the chapters, then grab first if no reading progress
    this.readChapter(libraryId, seriesId, [...volume.chapters].sort(this.utilityService.sortChapters)[0], incognitoMode);
  }

  readChapter(libraryId: number, seriesId: number, chapter: Chapter, incognitoMode: boolean = false) {
    if (chapter.pages === 0) {
      this.toastr.error(translate('series-detail.no-pages'));
      return;
    }

    this.router.navigate(this.getNavigationArray(libraryId, seriesId, chapter.id, chapter.files[0].format),
      {queryParams: {incognitoMode}});
  }
}
