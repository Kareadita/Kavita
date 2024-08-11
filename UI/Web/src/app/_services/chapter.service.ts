import { Injectable } from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {AccountService} from "./account.service";
import {UserCollection} from "../_models/collection-tag";
import {Chapter} from "../_models/chapter";
import {HourEstimateRange} from "../_models/series-detail/hour-estimate-range";

@Injectable({
  providedIn: 'root'
})
export class ChapterService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getChapterMetadata(chapterId: number) {
    return this.httpClient.get<Chapter>(this.baseUrl + 'chapter/?chapterId=' + chapterId);
  }

}
