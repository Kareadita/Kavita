import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { TextResonse } from 'src/app/_types/text-response';
import { environment } from 'src/environments/environment';
import { BookChapterItem } from '../_models/book-chapter-item';
import { BookInfo } from '../_models/book-info';

@Injectable({
  providedIn: 'root'
})
export class BookService {

  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getBookChapters(chapterId: number) {
    return this.http.get<Array<BookChapterItem>>(this.baseUrl + 'book/' + chapterId + '/chapters');
  }

  getBookPage(chapterId: number, page: number) {
    return this.http.get<string>(this.baseUrl + 'book/' + chapterId + '/book-page?page=' + page, TextResonse);
  }

  getBookInfo(chapterId: number) {
    return this.http.get<BookInfo>(this.baseUrl + 'book/' + chapterId + '/book-info');
  }

  getBookPageUrl(chapterId: number, page: number) {
    return this.baseUrl + 'book/' + chapterId + '/book-page?page=' + page;
  }
}
