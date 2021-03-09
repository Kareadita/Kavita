import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { forkJoin, Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { MangaFile } from '../_models/manga-file';
import { MangaImage } from '../_models/manga-image';

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

  // getPageInfo(chapterId: number, page: number) {
  //   // TODO: Deprecated
  //   return this.httpClient.get<MangaImage>(this.baseUrl + 'reader/image-info?chapterId=' + chapterId + '&page=' + page);
  // }

  getChapterPath(chapterId: number) {
    return this.httpClient.get(this.baseUrl + 'reader/chapter-path?chapterId=' + chapterId, {responseType: 'text'});
  }

  bookmark(seriesId: number, volumeId: number, chapterId: number, page: number) {
    return this.httpClient.post(this.baseUrl + 'reader/bookmark', {seriesId, volumeId, chapterId, pageNum: page});
  }

  markVolumeRead(volumeId: number) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-volume-read', {});
  }
}
