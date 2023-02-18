import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { TextResonse } from '../_types/text-response';

@Injectable({
  providedIn: 'root'
})
export class UploadService {

  private baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }


  uploadByUrl(url: string) {
    return this.httpClient.post<string>(this.baseUrl + 'upload/upload-by-url', {url}, TextResonse);
  }

  /**
   * 
   * @param seriesId Series to overwrite cover image for
   * @param url A base64 encoded url
   * @returns 
   */
  updateSeriesCoverImage(seriesId: number, url: string) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/series', {id: seriesId, url: this._cleanBase64Url(url)});
  }

  updateCollectionCoverImage(tagId: number, url: string) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/collection', {id: tagId, url: this._cleanBase64Url(url)});
  }

  updateReadingListCoverImage(readingListId: number, url: string) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/reading-list', {id: readingListId, url: this._cleanBase64Url(url)});
  }

  updateChapterCoverImage(chapterId: number, url: string) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/chapter', {id: chapterId, url: this._cleanBase64Url(url)});
  }

  updateLibraryCoverImage(libraryId: number, url: string) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/library', {id: libraryId, url: this._cleanBase64Url(url)});
  }

  resetChapterCoverLock(chapterId: number, ) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/reset-chapter-lock', {id: chapterId, url: ''});
  }

  _cleanBase64Url(url: string) {
    if (url.startsWith('data')) {
      url = url.split(',')[1];
    }
    return url;
  }
}
