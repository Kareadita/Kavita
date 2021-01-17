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

  getBookmark(volumeId: number) {
    return this.httpClient.get<number>(this.baseUrl + 'reader/get-bookmark?volumeId=' + volumeId);
  }

  getPage(volumeId: number, page: number) {
    return this.httpClient.get<MangaImage>(this.baseUrl + 'reader/image?volumeId=' + volumeId + '&page=' + page);
  }

  bookmark(seriesId: number, volumeId: number, page: number) {
    return this.httpClient.post(this.baseUrl + 'reader/bookmark', {seriesId, volumeId, pageNum: page});
  }
}
