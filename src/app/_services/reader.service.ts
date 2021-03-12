import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ReaderService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getBookmark(chapterId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'reader/get-bookmark?chapterId=' + chapterId);
  }

  getPageUrl(chapterId: number, page: number) {
    return this.baseUrl + 'reader/image?chapterId=' + chapterId + '&page=' + page;
  }

  getVolumeCoverImage(volumeId: number) {
    // TODO: If this works, refactor getPageUrl to same controller
    return this.baseUrl + 'image/volume-cover?volumeId=' + volumeId;
  }

  getSeriesCoverImage(seriesId: number) {
    // TODO: If this works, refactor getPageUrl to same controller
    return this.baseUrl + 'image/series-cover?seriesId=' + seriesId;
  }

  getChapterCoverImage(chapterId: number) {
    // TODO: If this works, refactor getPageUrl to same controller
    return this.baseUrl + 'image/chapter-cover?chapterId=' + chapterId;
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
}
