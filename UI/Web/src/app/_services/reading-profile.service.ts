import { Injectable } from '@angular/core';
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {ReadingProfile} from "../_models/preferences/reading-profiles";

@Injectable({
  providedIn: 'root'
})
export class ReadingProfileService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getForSeries(seriesId: number) {
    return this.httpClient.get<ReadingProfile>(this.baseUrl + "ReadingProfile/"+seriesId);
  }

  updateProfile(profile: ReadingProfile, seriesId?: number) {
    if (seriesId) {
      return this.httpClient.post(this.baseUrl + "ReadingProfile?seriesCtx="+seriesId, profile);
    }
    return this.httpClient.post(this.baseUrl + "ReadingProfile", profile);
  }

  updateImplicit(profile: ReadingProfile, seriesId: number) {
    return this.httpClient.post(this.baseUrl + "ReadingProfile/series?seriesId="+seriesId, profile);
  }

}
