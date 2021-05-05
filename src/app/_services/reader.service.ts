import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { UtilityService } from '../shared/_services/utility.service';
import { Chapter } from '../_models/chapter';
import { Volume } from '../_models/volume';

@Injectable({
  providedIn: 'root'
})
export class ReaderService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private utilityService: UtilityService) { }

  getBookmark(chapterId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'reader/get-bookmark?chapterId=' + chapterId);
  }

  getPageUrl(chapterId: number, page: number) {
    return this.baseUrl + 'reader/image?chapterId=' + chapterId + '&page=' + page;
  }

  getChapterPath(chapterId: number) {
    return this.httpClient.get(this.baseUrl + 'reader/chapter-path?chapterId=' + chapterId, {responseType: 'text'});
  }

  bookmark(seriesId: number, volumeId: number, chapterId: number, page: number) {
    return this.httpClient.post(this.baseUrl + 'reader/bookmark', {seriesId, volumeId, chapterId, pageNum: page});
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

  getCurrentVolumeAndChapter(volumes: Array<Volume>): [Volume | undefined, Chapter | undefined] {
    let currentlyReadingVolume: Volume | undefined = undefined;
    let currentlyReadingChapter: Chapter | undefined = undefined;
    const chapters = volumes.filter(v => v.number === 0).map(v => v.chapters || []).flat().sort(this.utilityService.sortChapters);

    for (let v of volumes) {
      if (v.number === 0) {
        continue;
      } else if (v.pagesRead >= v.pages) {
        continue;
      } else if (v.pagesRead < v.pages) {
        currentlyReadingVolume = v;
        break;
      }
    }

    if (currentlyReadingVolume === undefined) {
      // We need to check against chapters
      chapters.forEach(c => {
        if (c.pagesRead >= c.pages) {
          return;
        } else if (currentlyReadingChapter === undefined) {
          currentlyReadingChapter = c;
        }
      });
      if (currentlyReadingChapter === undefined) {
        // Default to first chapter
        currentlyReadingChapter = chapters[0];
      }
    }

    return [currentlyReadingVolume, currentlyReadingChapter];
  }
}
