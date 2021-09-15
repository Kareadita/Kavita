import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ChapterInfo } from '../manga-reader/_models/chapter-info';
import { UtilityService } from '../shared/_services/utility.service';
import { Chapter } from '../_models/chapter';
import { PageBookmark } from '../_models/page-bookmark';
import { ProgressBookmark } from '../_models/progress-bookmark';
import { Volume } from '../_models/volume';

@Injectable({
  providedIn: 'root'
})
export class ReaderService {

  baseUrl = environment.apiUrl;

  // Override background color for reader and restore it onDestroy
  private originalBodyColor!: string;

  constructor(private httpClient: HttpClient, private utilityService: UtilityService) { }

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

  getProgress(chapterId: number) {
    return this.httpClient.get<ProgressBookmark>(this.baseUrl + 'reader/get-progress?chapterId=' + chapterId);
  }

  getPageUrl(chapterId: number, page: number) {
    return this.baseUrl + 'reader/image?chapterId=' + chapterId + '&page=' + page;
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

  getCurrentChapter(volumes: Array<Volume>): Chapter {
    let currentlyReadingChapter: Chapter | undefined = undefined;
    const chapters = volumes.filter(v => v.number !== 0).map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters); 

    for (const c of chapters) {
      if (c.pagesRead < c.pages) {
        currentlyReadingChapter = c;
        break;
      }
    }

    if (currentlyReadingChapter === undefined) {
      // Check if there are specials we can load:
      const specials = volumes.filter(v => v.number === 0).map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters);
      for (const c of specials) {
        if (c.pagesRead < c.pages) {
          currentlyReadingChapter = c;
          break;
        }
      }
      if (currentlyReadingChapter === undefined) {
        // Default to first chapter
        currentlyReadingChapter = chapters[0];
      }
    }

    return currentlyReadingChapter;
  }

  /**
   * Captures current body color and forces background color to be black. Call @see resetOverrideStyles() on destroy of component to revert changes
   */
  setOverrideStyles() {
    const bodyNode = document.querySelector('body');
    if (bodyNode !== undefined && bodyNode !== null) {
      this.originalBodyColor = bodyNode.style.background;
      bodyNode.setAttribute('style', 'background-color: black !important');
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
}
