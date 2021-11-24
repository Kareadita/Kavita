import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ChapterMetadata } from '../_models/chapter-metadata';

@Injectable({
  providedIn: 'root'
})
export class MetadataService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getChapterMetadata(chapterId: number) {
    return this.httpClient.get<ChapterMetadata>(this.baseUrl + 'series/chapter-metadata?chapterId=' + chapterId);
  }
}
