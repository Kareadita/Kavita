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

  updateProfile(profile: ReadingProfile) {
    return this.httpClient.post(this.baseUrl + "ReadingProfile", profile);
  }

  createProfile(profile: ReadingProfile) {
    return this.httpClient.post<ReadingProfile>(this.baseUrl + "ReadingProfile/create", profile);
  }

  updateImplicit(profile: ReadingProfile, seriesId: number) {
    return this.httpClient.post(this.baseUrl + "ReadingProfile/series?seriesId="+seriesId, profile);
  }

  all() {
    return this.httpClient.get<ReadingProfile[]>(this.baseUrl + "ReadingProfile/all");
  }

  delete(id: number) {
    return this.httpClient.delete(this.baseUrl + "ReadingProfile?profileId="+id);
  }

  setDefault(id: number) {
    return this.httpClient.post(this.baseUrl + "ReadingProfile/set-default?profileId=" + id, {});
  }

  addToSeries(id: number, seriesId: number) {
    return this.httpClient.post(this.baseUrl + `ReadingProfile/series/${seriesId}?profileId=${id}`, {});
  }

  clearSeriesProfiles(seriesId: number) {
    return this.httpClient.delete(this.baseUrl + `ReadingProfile/series/${seriesId}`, {});
  }

  addToLibrary(id: number, libraryId: number) {
    return this.httpClient.post(this.baseUrl + `ReadingProfile/library/${libraryId}?profileId=${id}`, {});
  }

  clearLibraryProfiles(libraryId: number) {
    return this.httpClient.delete(this.baseUrl + `ReadingProfile/library/${libraryId}`, {});
  }

  bulkAddToSeries(id: number, seriesIds: number[]) {
    return this.httpClient.post(this.baseUrl + `ReadingProfile/bulk?profileId=${id}`, seriesIds);
  }

}
