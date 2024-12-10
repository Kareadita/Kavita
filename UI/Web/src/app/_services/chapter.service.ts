import { Injectable } from '@angular/core';
import {environment} from "../../environments/environment";
import { HttpClient } from "@angular/common/http";
import {Chapter} from "../_models/chapter";
import {TextResonse} from "../_types/text-response";

@Injectable({
  providedIn: 'root'
})
export class ChapterService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getChapterMetadata(chapterId: number) {
    return this.httpClient.get<Chapter>(this.baseUrl + 'chapter?chapterId=' + chapterId);
  }

  deleteChapter(chapterId: number) {
    return this.httpClient.delete<boolean>(this.baseUrl + 'chapter?chapterId=' + chapterId);
  }

  deleteMultipleChapters(seriesId: number, chapterIds: Array<number>) {
    return this.httpClient.post<boolean>(this.baseUrl + `chapter/delete-multiple?seriesId=${seriesId}`, {chapterIds});
  }

  updateChapter(chapter: Chapter) {
    return this.httpClient.post(this.baseUrl + 'chapter/update', chapter, TextResonse);
  }

}
