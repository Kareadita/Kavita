import {HttpClient} from '@angular/common/http';
import {DestroyRef, effect, inject, Injectable} from '@angular/core';
import {DOCUMENT, Location} from '@angular/common';
import {Router} from '@angular/router';
import {environment} from 'src/environments/environment';
import {ChapterInfo} from '../manga-reader/_models/chapter-info';
import {Chapter} from '../_models/chapter';
import {HourEstimateRange} from '../_models/series-detail/hour-estimate-range';
import {MangaFormat} from '../_models/manga-format';
import {BookmarkInfo} from '../_models/manga-reader/bookmark-info';
import {PageBookmark} from '../_models/readers/page-bookmark';
import {ProgressBookmark} from '../_models/readers/progress-bookmark';
import {FileDimension} from '../manga-reader/_models/file-dimension';
import screenfull from 'screenfull';
import {TextResonse} from '../_types/text-response';
import {AccountService} from './account.service';
import {PersonalToC} from "../_models/readers/personal-toc";
import {FilterV2} from "../_models/metadata/v2/filter-v2";
import NoSleep from 'nosleep.js';
import {FullProgress} from "../_models/readers/full-progress";
import {Volume} from "../_models/volume";
import {UtilityService} from "../shared/_services/utility.service";
import {translate} from "@jsverse/transloco";
import {ToastrService} from "ngx-toastr";
import {FilterField} from "../_models/metadata/v2/filter-field";
import {ModalService} from "./modal.service";
import {filter, map, Observable, of, switchMap, tap} from "rxjs";
import {ListSelectModalComponent} from "../shared/_components/list-select-modal/list-select-modal.component";
import {take, takeUntil} from "rxjs/operators";
import {SeriesService} from "./series.service";
import {Series} from "../_models/series";

enum RereadPromptResult {
  Cancel = 0,
  Reread = 1,
  ReadIncognito = 3,
  Continue = 4,
}

export const CHAPTER_ID_DOESNT_EXIST = -1;
export const CHAPTER_ID_NOT_FETCHED = -2;

const MS_IN_DAY = 1000 * 60 * 60 * 24;

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
  private readonly httpClient = inject(HttpClient);
  private readonly document = inject(DOCUMENT);
  private readonly modalService = inject(ModalService);
  private readonly seriesService = inject(SeriesService);

  baseUrl = environment.apiUrl;
  encodedKey: string = '';

  // Override background color for reader and restore it onDestroy
  private originalBodyColor!: string;


  private noSleep: NoSleep = new NoSleep();

  constructor() {
    effect(() => {
      const apiKey = this.accountService.currentUserGenericApiKey();
      if (apiKey) {
        this.encodedKey = encodeURIComponent(apiKey);
      }
    })
  }


  enableWakeLock(element?: Element | Document) {
    // Enable wake lock.
    // (must be wrapped in a user input event handler e.g. a mouse or touch handler)

    if (!element) element = this.document;

    const enableNoSleepHandler = async () => {
      element!.removeEventListener('click', enableNoSleepHandler, false);
      element!.removeEventListener('touchmove', enableNoSleepHandler, false);
      element!.removeEventListener('mousemove', enableNoSleepHandler, false);
      await this.noSleep.enable();
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

  bookmark(seriesId: number, volumeId: number, chapterId: number, page: number, imageNumber: number = 0, xpath: string | null = null) {
    return this.httpClient.post(this.baseUrl + 'reader/bookmark', {seriesId, volumeId, chapterId, page, imageNumber, xpath});
  }

  unbookmark(seriesId: number, volumeId: number, chapterId: number, page: number, imageNumber: number = 0) {
    return this.httpClient.post(this.baseUrl + 'reader/unbookmark', {seriesId, volumeId, chapterId, page, imageNumber});
  }

  getAllBookmarks(filter: FilterV2<FilterField> | undefined) {
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

  getTimeLeftForChapter(seriesId: number, chapterId: number) {
    return this.httpClient.get<HourEstimateRange>(this.baseUrl + `reader/time-left-for-chapter?seriesId=${seriesId}&chapterId=${chapterId}`);
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
    const params: {[key: string]: any} = {};
    params['incognitoMode'] = incognitoMode;

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
  closeReader(libraryId: number, seriesId: number, chapterId: number, readingListMode: boolean = false, readingListId: number = 0) {
    if (readingListMode) {
      this.router.navigateByUrl('lists/' + readingListId);
      return
    }

    if (window.history.length > 1) {
      this.location.back();
      return;
    }

    this.router.navigateByUrl(`/library/${libraryId}/series/${seriesId}/chapter/${chapterId}`);
  }

  removePersonalToc(chapterId: number, pageNumber: number, title: string) {
    return this.httpClient.delete(this.baseUrl + `reader/ptoc?chapterId=${chapterId}&pageNum=${pageNumber}&title=${encodeURIComponent(title)}`);
  }

  getPersonalToC(chapterId: number) {
    return this.httpClient.get<Array<PersonalToC>>(this.baseUrl + 'reader/ptoc?chapterId=' + chapterId);
  }

  createPersonalToC(libraryId: number, seriesId: number, volumeId: number, chapterId: number, pageNumber: number, title: string, bookScrollId: string | null, selectedText: string) {
    return this.httpClient.post(this.baseUrl + 'reader/create-ptoc', {libraryId, seriesId, volumeId, chapterId, pageNumber, title, bookScrollId, selectedText});
  }



  getElementFromXPath(path: string) {
    try {
      const node = document.evaluate(path, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;
      if (node?.nodeType === Node.ELEMENT_NODE) {
        return node as Element;
      }
      return null;
    } catch (e) {
      console.debug("Failed to evaluate XPath:", path, " exception:", e)
      return null;
    }
  }

  /**
   * Removes the Kavita UI aspect of the xpath from a given xpath variable
   * Used for Annotations and Bookmarks within epub reader
   *
   * @param xpath
   */
  descopeBookReaderXpath(xpath: string) {
    if (xpath.startsWith("id(")) return xpath;

    const bookContentElement = this.document.querySelector('.book-content');
    if (!bookContentElement?.children[0]) {
      console.warn('Book content element not found, returning original xpath');
      return xpath;
    }

    const bookContentXPath = this.getXPathTo(bookContentElement.children[0], true);

    // Normalize both paths
    const normalizedXpath = this.normalizeXPath(xpath);
    const normalizedBookContentXPath = this.normalizeXPath(bookContentXPath);

    //console.log('Descoping - Original:', xpath);
    //console.log('Descoping - Normalized xpath:', normalizedXpath);
    //console.log('Descoping - Book content path:', normalizedBookContentXPath);

    // Find the UI container pattern and extract content path
    const descopedPath = this.extractContentPath(normalizedXpath, normalizedBookContentXPath);

    //console.log('Descoped', xpath, 'to', descopedPath);
    return descopedPath;
  }

  /**
   * Adds the Kavita UI aspect to the xpath so loading from xpath in the reader works
   * @param xpath
   */
  scopeBookReaderXpath(xpath: string) {
    if (xpath.startsWith("id(")) return xpath;

    const bookContentElement = this.document.querySelector('.book-content');
    if (!bookContentElement?.children[0]) {
      console.warn('Book content element not found, returning original xpath');
      return xpath;
    }

    const bookContentXPath = this.getXPathTo(bookContentElement.children[0], true);
    const normalizedXpath = this.normalizeXPath(xpath);
    const normalizedBookContentXPath = this.normalizeXPath(bookContentXPath);

    // If already scoped, return as-is
    if (normalizedXpath.includes(normalizedBookContentXPath)) {
      return xpath;
    }

    // Replace //body with the actual book content path
    if (normalizedXpath.startsWith('//body')) {
      const relativePath = normalizedXpath.substring(6); // Remove '//body'
      return bookContentXPath + relativePath;
    }

    // If it starts with /body, replace with book content path
    if (normalizedXpath.startsWith('/body')) {
      const relativePath = normalizedXpath.substring(5); // Remove '/body'
      return bookContentXPath + relativePath;
    }

    // Default: prepend the book content path
    return bookContentXPath + (normalizedXpath.startsWith('/') ? normalizedXpath : '/' + normalizedXpath);
  }

  /**
   * Extract the content path by finding the UI container boundary
   */
  private extractContentPath(fullXpath: string, bookContentXPath: string): string {
    // Look for the pattern where the book content container ends
    // The book content path should be a prefix of the full path

    // First, try direct substring match
    if (fullXpath.startsWith(bookContentXPath)) {
      const contentPath = fullXpath.substring(bookContentXPath.length);
      return '//body' + (contentPath.startsWith('/') ? contentPath : '/' + contentPath);
    }

    // If direct match fails, try to find the common UI structure pattern
    // Look for the app-book-reader container end point
    const readerPattern = /\/app-book-reader\[\d+\]\/div\[\d+\]\/div\[\d+\]\/div\[\d+\]\/div\[\d+\]\/div\[\d+\]/;
    const match = fullXpath.match(readerPattern);

    if (match) {
      const containerEndIndex = fullXpath.indexOf(match[0]) + match[0].length;
      const contentPath = fullXpath.substring(containerEndIndex);
      return '//body' + (contentPath.startsWith('/') ? contentPath : '/' + contentPath);
    }

    // Alternative approach: look for the deepest common path structure
    // Split both paths and find where they diverge after the UI container
    const fullParts = fullXpath.split('/').filter(p => p.length > 0);
    const bookParts = bookContentXPath.split('/').filter(p => p.length > 0);

    // Find the app-book-reader index in full path
    const readerIndex = fullParts.findIndex(part => part.startsWith('app-book-reader'));

    if (readerIndex !== -1) {
      // Look for the pattern after app-book-reader that matches book content structure
      // Typically: app-book-reader[1]/div[1]/div[2]/div[3]/div[1]/div[1] then content starts
      let contentStartIndex = readerIndex + 6; // Skip the typical 6 div containers

      // Adjust based on actual book content depth
      const bookReaderIndex = bookParts.findIndex(part => part.startsWith('app-book-reader'));
      if (bookReaderIndex !== -1) {
        const expectedDepth = bookParts.length - bookReaderIndex - 1;
        contentStartIndex = readerIndex + 1 + expectedDepth;
      }

      if (contentStartIndex < fullParts.length) {
        const contentParts = fullParts.slice(contentStartIndex);
        return '//body/' + contentParts.join('/');
      }
    }

    // Fallback: clean common UI prefixes
    return this.cleanCommonUIPrefixes(fullXpath);
  }

  /**
   * Normalize XPath by cleaning common variations and converting to lowercase
   */
  private normalizeXPath(xpath: string): string {
    let normalized = xpath.toLowerCase();

    // Remove common HTML document prefixes
    const prefixesToRemove = [
      '//html[1]//body',
      '//html[1]//app-root[1]',
      '//html//body',
      '//html//app-root[1]'
    ];

    for (const prefix of prefixesToRemove) {
      if (normalized.startsWith(prefix)) {
        normalized = '//body' + normalized.substring(prefix.length);
        break;
      }
    }

    return normalized;
  }

  /**
   * Clean common UI prefixes that shouldn't be in descoped paths
   */
  private cleanCommonUIPrefixes(xpath: string): string {
    let cleaned = xpath;

    // Remove app-root references
    cleaned = cleaned.replace(/\/app-root\[\d+\]/g, '');

    // Ensure it starts with //body
    if (!cleaned.startsWith('//body') && !cleaned.startsWith('/body')) {
      // Try to find body in the path
      const bodyIndex = cleaned.indexOf('/body');
      if (bodyIndex !== -1) {
        cleaned = '//' + cleaned.substring(bodyIndex + 1);
      } else {
        // If no body found, assume it should start with //body
        cleaned = '//body' + (cleaned.startsWith('/') ? cleaned : '/' + cleaned);
      }
    }

    return cleaned;
  }


  /**
   *
   * @param element
   * @param pureXPath Will ignore shortcuts like id('')
   */
  getXPathTo(element: any, pureXPath = false): string {
    if (!element) {
      console.error('getXPathTo: element is null or undefined');
      return '';
    }

    let xpath = this.getXPath(element, pureXPath);

    // Ensure xpath starts with // for absolute paths
    if (xpath && !xpath.startsWith('//') && !xpath.startsWith('id(')) {
      xpath = '//' + xpath;
    }

    return xpath;
  }

  private getXPath(element: HTMLElement, pureXPath = false): string {
    if (!element) {
      console.error('getXPath: element is null or undefined');
      return '';
    }

    // Handle shortcuts (unless pureXPath is requested)
    if (!pureXPath && element.id) {
      return `id("${element.id}")`;
    }

    if (element === document.body) {
      return 'body';
    }

    if (!element.parentNode) {
      return element.tagName.toLowerCase();
    }

    // Count same-tag siblings
    let siblingIndex = 1;
    const siblings = Array.from(element.parentNode?.children ?? []);
    const tagName = element.tagName;

    for (const sibling of siblings) {
      if (sibling === element) {
        break;
      }
      if (sibling.tagName === tagName) {
        siblingIndex++;
      }
    }

    const currentPath = `${element.tagName.toLowerCase()}[${siblingIndex}]`;
    const parentPath = this.getXPath(element.parentElement!, pureXPath);

    return parentPath ? `${parentPath}/${currentPath}` : currentPath;
  }

  private handleRereadPrompt(
    type: 'series' | 'volume' | 'chapter',
    target: Chapter,
    incognitoMode: boolean,
    onReread: () => Observable<any>
  ): Observable<{ shouldContinue: boolean; incognitoMode: boolean }> {
    return this.promptForReread(type, target, incognitoMode).pipe(
      switchMap(result => {
        switch (result) {
          case RereadPromptResult.Cancel:
            return of({ shouldContinue: false, incognitoMode });
          case RereadPromptResult.Continue:
            return of({ shouldContinue: true, incognitoMode });
          case RereadPromptResult.ReadIncognito:
            return of({ shouldContinue: true, incognitoMode: true });
          case RereadPromptResult.Reread:
            return onReread().pipe(map(() => ({ shouldContinue: true, incognitoMode })));
        }
      })
    );
  }

  readSeries(series: Series, incognitoMode: boolean = false, callback?: (chapter: Chapter) => void) {
    const fullyRead = series.pagesRead >= series.pages;
    const shouldPromptForReread = fullyRead && !incognitoMode;

    if (!shouldPromptForReread) {
      this.getCurrentChapter(series.id).subscribe(chapter => {
        this.readChapter(series.libraryId, series.id, chapter, incognitoMode);
      });
      return;
    }

    this.getCurrentChapter(series.id).pipe(
      switchMap(chapter =>
        this.handleRereadPrompt('series', chapter, incognitoMode,
          () => this.seriesService.markUnread(series.id)).pipe(
            map(result => ({ chapter, result }))
        )),
      filter(({ result }) => result.shouldContinue),
      tap(({ chapter, result }) => {
        if (callback) {
          callback(chapter);
        }

        this.readChapter(series.libraryId, series.id, chapter, result.incognitoMode, false);
      })
    ).subscribe();
  }

  readVolume(libraryId: number, seriesId: number, volume: Volume, incognitoMode: boolean = false) {
    if (volume.chapters.length === 0) return;

    const sortedChapters = [...volume.chapters].sort(this.utilityService.sortChapters);

    // No reading progress or incognito - start from first chapter
    if (volume.pagesRead === 0 || incognitoMode) {
      this.readChapter(libraryId, seriesId, sortedChapters[0], incognitoMode);
      return;
    }

    // Not fully read - continue from current position
    if (volume.pagesRead < volume.pages) {
      const unreadChapters = volume.chapters.filter(item => item.pagesRead < item.pages);
      const chapterToRead = unreadChapters.length > 0 ? unreadChapters[0] : sortedChapters[0];
      this.readChapter(libraryId, seriesId, chapterToRead, incognitoMode);
      return;
    }

    // Fully read - prompt for reread
    const lastChapter = volume.chapters[volume.chapters.length - 1];
    this.handleRereadPrompt('volume', lastChapter, incognitoMode,
      () => this.markVolumeUnread(seriesId, volume.id)).pipe(
        filter(result => result.shouldContinue),
      tap(result => {
        this.readChapter(libraryId, seriesId, sortedChapters[0], result.incognitoMode, false);
      })
    ).subscribe();
  }

  readChapter(libraryId: number, seriesId: number, chapter: Chapter, incognitoMode: boolean = false, promptForReread: boolean = true) {
    if (chapter.pages === 0) {
      this.toastr.error(translate('series-detail.no-pages'));
      return;
    }

    const navigateToReader = (useIncognitoMode: boolean) => {
      this.router.navigate(
        this.getNavigationArray(libraryId, seriesId, chapter.id, chapter.files[0].format),
        { queryParams: { incognitoMode: useIncognitoMode } }
      );
    };

    if (!promptForReread) {
      navigateToReader(incognitoMode);
      return;
    }

    this.handleRereadPrompt('chapter', chapter, incognitoMode,
      () => this.saveProgress(libraryId, seriesId, chapter.volumeId, chapter.id, 0)
    ).pipe(
      filter(result => result.shouldContinue),
      tap(result => navigateToReader(result.incognitoMode))
    ).subscribe();
  }

  promptForReread(entityType: 'series' | 'volume' | 'chapter', chapter: Chapter, incognitoMode: boolean) {
    if (!this.shouldPromptForReread(chapter, incognitoMode)) return of(RereadPromptResult.Continue);

    const fullyRead = chapter.pagesRead >= chapter.pages;

    const [modal, component] = this.modalService.open(ListSelectModalComponent, {
      centered: true,
    });

    component.showFooter.set(false);
    component.title.set(translate('reread-modal.title'));

    const daysSinceRead = Math.round((new Date().getTime() - new Date(chapter.lastReadingProgress).getTime()) / MS_IN_DAY);

    if (chapter.pagesRead >= chapter.pages) {
      component.description.set(translate('reread-modal.description-full-read', { entityType: translate('entity-type.' + entityType) }));
    } else {
      component.description.set(translate('reread-modal.description-time-passed', { days: daysSinceRead, entityType: translate('entity-type.' + entityType) }));
    }

    const options = [
      {label: translate('reread-modal.reread'), value: RereadPromptResult.Reread},
      {label: translate('reread-modal.continue'), value: RereadPromptResult.Continue},
    ];

    if (fullyRead) {
      options.push({label: translate('reread-modal.read-incognito'), value: RereadPromptResult.ReadIncognito});
    }

    options.push({label: translate('reread-modal.cancel'), value: RereadPromptResult.Cancel});

    component.items.set(options);

    return modal.closed.pipe(
      takeUntil(modal.dismissed),
      take(1),
      map(res => res as RereadPromptResult),
    );
  }

  private shouldPromptForReread(chapter: Chapter, incognitoMode: boolean) {
    if (incognitoMode || chapter.pagesRead === 0) return false;
    if (chapter.pagesRead >= chapter.pages) return true;

    const userPreferences = this.accountService.currentUserSignal()!.preferences;

    if (!userPreferences.promptForRereadsAfter) {
      return false;
    }

    const daysSinceRead = (new Date().getTime() - new Date(chapter.lastReadingProgress).getTime()) / MS_IN_DAY;
    return daysSinceRead > userPreferences.promptForRereadsAfter;
  }

}
