import {inject, Injectable} from '@angular/core';
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {ReadingProfile} from "../_models/preferences/reading-profiles";

@Injectable({
  providedIn: 'root'
})
export class ReadingProfileService {

  private readonly httpClient = inject(HttpClient);
  baseUrl = environment.apiUrl;

  getForSeries(libraryId: number, seriesId: number, skipImplicit: boolean = false) {
    return this.httpClient.get<ReadingProfile>(this.baseUrl + `reading-profile/${libraryId}/${seriesId}?skipImplicit=${skipImplicit}`);
  }

  getAllForSeries(seriesId: number) {
    return this.httpClient.get<ReadingProfile[]>(this.baseUrl + `reading-profile/series?seriesId=${seriesId}`);
  }

  getForLibrary(libraryId: number) {
    return this.httpClient.get<ReadingProfile[]>(this.baseUrl + `reading-profile/library?libraryId=${libraryId}`);
  }

  updateProfile(profile: ReadingProfile) {
    return this.httpClient.post<ReadingProfile>(this.baseUrl + 'reading-profile', profile);
  }

  updateParentProfile(libraryId: number, seriesId: number, profile: ReadingProfile) {
    return this.httpClient.post<ReadingProfile>(this.baseUrl + `reading-profile/update-parent?seriesId=${seriesId}&libraryId=${libraryId}`, profile);
  }

  createProfile(profile: ReadingProfile) {
    return this.httpClient.post<ReadingProfile>(this.baseUrl + 'reading-profile/create', profile);
  }

  promoteProfile(profileId: number) {
    return this.httpClient.post<ReadingProfile>(this.baseUrl + "reading-profile/promote?profileId=" + profileId, {});
  }

  updateImplicit(libraryId: number, seriesId: number, profile: ReadingProfile) {
    return this.httpClient.post<ReadingProfile>(this.baseUrl + `reading-profile/series?seriesId=${seriesId}&libraryId=${libraryId}`, profile);
  }

  getAllProfiles() {
    return this.httpClient.get<ReadingProfile[]>(this.baseUrl + 'reading-profile/all');
  }

  delete(id: number) {
    return this.httpClient.delete(this.baseUrl + `reading-profile?profileId=${id}`);
  }

  addToSeries(id: number, seriesId: number) {
    return this.httpClient.post(this.baseUrl + `reading-profile/series/${seriesId}?profileId=${id}`, {});
  }

  clearSeriesProfiles(seriesId: number) {
    return this.httpClient.delete(this.baseUrl + `reading-profile/series/${seriesId}`, {});
  }

  addToLibrary(id: number, libraryId: number) {
    return this.httpClient.post(this.baseUrl + `reading-profile/library/${libraryId}?profileId=${id}`, {});
  }

  clearLibraryProfiles(libraryId: number) {
    return this.httpClient.delete(this.baseUrl + `reading-profile/library/${libraryId}`, {});
  }

  bulkAddToSeries(id: number, seriesIds: number[]) {
    return this.httpClient.post(this.baseUrl + `reading-profile/bulk?profileId=${id}`, seriesIds);
  }

  setDevices(id: number, deviceIds: number[]) {
    return this.httpClient.post(this.baseUrl + `reading-profile/set-devices?profileId=${id}`, deviceIds);
  }

}
