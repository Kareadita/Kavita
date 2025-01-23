import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { TextResonse } from 'src/app/_types/text-response';
import { environment } from 'src/environments/environment';
import { BookChapterItem } from '../_models/book-chapter-item';
import { BookInfo } from '../_models/book-info';

export interface FontFamily {
  /**
   * What the user should see
   */
  title: string;
  /**
   * The actual font face
   */
  family: string;
}

@Injectable({
  providedIn: 'root'
})
export class BookService {

  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getFontFamilies(): Array<FontFamily> {
    return [{title: 'default', family: 'default'}, {title: 'EBGaramond', family: 'EBGaramond'}, {title: 'Fira Sans', family: 'Fira_Sans'},
    {title: 'Lato', family: 'Lato'}, {title: 'Libre Baskerville', family: 'Libre_Baskerville'}, {title: 'Merriweather', family: 'Merriweather'},
    {title: 'Nanum Gothic', family: 'Nanum_Gothic'}, {title: 'Open Dyslexic', family: 'OpenDyslexic2'}, {title: 'RocknRoll One', family: 'RocknRoll_One'}, {title: 'Fast Font Serif (Bionic)', family: 'FastFontSerif'}, {title: 'Fast Font Sans (Bionic)', family: 'FastFontSans'}];
  }

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
