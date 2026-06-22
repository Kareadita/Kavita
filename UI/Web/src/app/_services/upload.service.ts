import {HttpClient} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {environment} from 'src/environments/environment';
import {TextResonse} from '../_types/text-response';
import {translate} from "@jsverse/transloco";
import {ToastrService} from "ngx-toastr";
import {tap} from "rxjs";

@Injectable({
  providedIn: 'root'
})
export class UploadService {
  private httpClient = inject(HttpClient);
  private readonly toastr = inject(ToastrService);

  private baseUrl = environment.apiUrl;


  uploadByUrl(url: string,  isInternalUrl = false) {
    return this.httpClient.post<string>(this.baseUrl + 'upload/upload-by-url', {url, isInternalUrl}, TextResonse);
  }

  /**
   * Stages a local file in the temp directory and returns its filename for use with the cover update endpoints.
   * @param file The image file to upload
   */
  uploadByFile(file: File) {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.httpClient.post<string>(this.baseUrl + 'upload/upload-by-file', formData, TextResonse);
  }

  /**
   *
   * @param seriesId Series to overwrite cover image for
   * @param fileName A temp filename returned by uploadByUrl/uploadByFile
   * @param lockCover Should the cover be locked or not
   * @returns
   */
  updateSeriesCoverImage(seriesId: number, fileName: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/series', {id: seriesId, fileName, lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateCollectionCoverImage(tagId: number, fileName: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/collection', {id: tagId, fileName, lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateReadingListCoverImage(readingListId: number, fileName: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/reading-list', {id: readingListId, fileName, lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateChapterCoverImage(chapterId: number, fileName: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/chapter', {id: chapterId, fileName, lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateVolumeCoverImage(volumeId: number, fileName: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/volume', {id: volumeId, fileName, lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateLibraryCoverImage(libraryId: number, fileName: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/library', {id: libraryId, fileName, lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updatePersonCoverImage(personId: number, fileName: string, lockCover: boolean = true) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/person', {id: personId, fileName, lockCover}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  updateUserCoverImage(userId: number, url: string) {
    return this.httpClient.post<number>(this.baseUrl + 'upload/user', {id: userId, url: this._cleanBase64Url(url), lockCover: false}).pipe(tap(_ => {
      this.toastr.info(translate('series-detail.cover-change'));
    }));
  }

  _cleanBase64Url(url: string) {
    if (url.startsWith('data')) {
      url = url.split(',')[1];
    }
    return url;
  }
}
