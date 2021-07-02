import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { BookChapterItem } from './_models/book-chapter-item';

export interface BookPage {
  bookTitle: string;
  styles: string;
  html: string;
}


@Injectable({
  providedIn: 'root'
})
export class BookService {

  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getFontFamilies() {
    return ['default', 'EBGaramond', 'Fira Sans', 'Lato', 'Libre Baskerville', 'Merriweather', 'Nanum Gothic', 'RocknRoll One'];
  }

  getBookChapters(chapterId: number) {
    return this.http.get<Array<BookChapterItem>>(this.baseUrl + 'book/' + chapterId + '/chapters');
  }

  getBookPage(chapterId: number, page: number) {
    return this.http.get<string>(this.baseUrl + 'book/' + chapterId + '/book-page?page=' + page, {responseType: 'text' as 'json'});
  }

  getBookInfo(chapterId: number) {
    return this.http.get<string>(this.baseUrl + 'book/' + chapterId + '/book-info', {responseType: 'text' as 'json'});
  }

  getBookPageUrl(chapterId: number, page: number) {
    return this.baseUrl + 'book/' + chapterId + '/book-page?page=' + page;
  }
}
