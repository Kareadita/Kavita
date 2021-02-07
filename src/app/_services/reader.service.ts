import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
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

  getPage(chapterId: number, page: number) {
    return this.httpClient.get<MangaImage>(this.baseUrl + 'reader/image?chapterId=' + chapterId + '&page=' + page);
  }

  bookmark(seriesId: number, volumeId: number, chapterId: number, page: number) {
    return this.httpClient.post(this.baseUrl + 'reader/bookmark', {seriesId, volumeId, chapterId, pageNum: page});
  }

  markVolumeRead(volumeId: number) {
    return this.httpClient.post(this.baseUrl + 'reader/mark-volume-read', {});
  }
}
