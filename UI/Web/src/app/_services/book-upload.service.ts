import {HttpClient} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {environment} from 'src/environments/environment';
import {BookUploadOptions} from '../_models/book-upload/book-upload-options';
import {BookUploadRequest} from '../_models/book-upload/book-upload-request';
import {BookUploadResponse} from '../_models/book-upload/book-upload-result';

@Injectable({
  providedIn: 'root'
})
export class BookUploadService {
  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getOptions(libraryId: number) {
    return this.httpClient.get<BookUploadOptions>(this.baseUrl + 'book-upload/options?libraryId=' + libraryId);
  }

  uploadFiles(request: BookUploadRequest, files: File[]) {
    const formData = new FormData();
    formData.append('libraryId', request.libraryId + '');
    formData.append('libraryFolder', request.libraryFolder);
    formData.append('targetFolderName', request.targetFolderName ?? '');
    formData.append('conflictMode', request.conflictMode + '');

    for (const file of files) {
      formData.append('files', file, file.name);
    }

    return this.httpClient.post<BookUploadResponse>(this.baseUrl + 'book-upload/files', formData);
  }
}
