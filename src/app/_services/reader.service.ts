import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { UtilityService } from '../shared/_services/utility.service';
import { Bookmark } from '../_models/bookmark';
import { Chapter } from '../_models/chapter';
import { Volume } from '../_models/volume';

@Injectable({
  providedIn: 'root'
})
export class ReaderService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private utilityService: UtilityService) { }

  getBookmark(chapterId: number) {
    return this.httpClient.get<Bookmark>(this.baseUrl + 'reader/get-bookmark?chapterId=' + chapterId);
  }

  getPageUrl(chapterId: number, page: number) {
    return this.baseUrl + 'reader/image?chapterId=' + chapterId + '&page=' + page;
  }

  getChapterPath(chapterId: number) {
    return this.httpClient.get(this.baseUrl + 'reader/chapter-path?chapterId=' + chapterId, {responseType: 'text'});
  }

  bookmark(seriesId: number, volumeId: number, chapterId: number, page: number, bookScrollId: string | null = null) {
    return this.httpClient.post(this.baseUrl + 'reader/bookmark', {seriesId, volumeId, chapterId, pageNum: page, bookScrollId});
  }

  markVolumeRead(seriesId: number, volumeId: number) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-volume-read', {seriesId, volumeId});
  }

  getNextChapter(seriesId: number, volumeId: number, currentChapterId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'reader/next-chapter?seriesId=' + seriesId + '&volumeId=' + volumeId + '&currentChapterId=' + currentChapterId);
  }

  getPrevChapter(seriesId: number, volumeId: number, currentChapterId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'reader/prev-chapter?seriesId=' + seriesId + '&volumeId=' + volumeId + '&currentChapterId=' + currentChapterId);
  }

  getCurrentChapter(volumes: Array<Volume>): Chapter {
    let currentlyReadingChapter: Chapter | undefined = undefined;
    const chapters = volumes.filter(v => v.number !== 0).map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters); // changed from === 0 to != 0

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
}
