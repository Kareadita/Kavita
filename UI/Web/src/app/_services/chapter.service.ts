import { Injectable, inject } from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {Chapter} from "../_models/chapter";
import {TextResonse} from "../_types/text-response";
import {ChapterDetailPlus} from "../_models/chapter-detail-plus";

@Injectable({
  providedIn: 'root'
})
export class ChapterService {
  private httpClient = inject(HttpClient);


  baseUrl = environment.apiUrl;

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

  chapterDetailPlus(seriesId: number, chapterId: number) {
    return this.httpClient.get<ChapterDetailPlus>(this.baseUrl + `chapter/chapter-detail-plus?chapterId=${chapterId}&seriesId=${seriesId}`);
  }

}
