import { HttpClient } from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import { environment } from 'src/environments/environment';
import { TextResonse } from '../_types/text-response';
import {translate} from "@jsverse/transloco";
import {ToastrService} from "ngx-toastr";
import {tap} from "rxjs";

@Injectable({
  providedIn: 'root'
})
export class UploadService {

  private baseUrl = environment.apiUrl;
  private readonly toastr = inject(ToastrService);

  constructor(private httpClient: HttpClient) { }


  uploadByUrl(url: string) {
    return this.httpClient.post<string>(this.baseUrl + 'upload/upload-by-url', {url}, TextResonse);
  }

  /**
   *
   * @param seriesId Series to overwrite cover image for
   * @param url A base64 encoded url
   * @param lockCover Should the cover be locked or not
   * @returns
   */
  updateSeriesCoverImage(seriesId: number, url: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/series', {id: seriesId, url: this._cleanBase64Url(url), lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateCollectionCoverImage(tagId: number, url: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/collection', {id: tagId, url: this._cleanBase64Url(url), lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateReadingListCoverImage(readingListId: number, url: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/reading-list', {id: readingListId, url: this._cleanBase64Url(url), lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateChapterCoverImage(chapterId: number, url: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/chapter', {id: chapterId, url: this._cleanBase64Url(url), lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateVolumeCoverImage(volumeId: number, url: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/volume', {id: volumeId, url: this._cleanBase64Url(url), lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateLibraryCoverImage(libraryId: number, url: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/library', {id: libraryId, url: this._cleanBase64Url(url), lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updatePersonCoverImage(personId: number, url: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/person', {id: personId, url: this._cleanBase64Url(url), lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
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
