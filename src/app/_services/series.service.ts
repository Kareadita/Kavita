import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Chapter } from '../_models/chapter';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';

@Injectable({
  providedIn: 'root'
})
export class SeriesService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getSeriesForLibrary(libraryId: number) {
    return this.httpClient.get<Series[]>(this.baseUrl + 'library/series?libraryId=' + libraryId);
  }

  getSeries(seriesId: number) {
    return this.httpClient.get<Series>(this.baseUrl + 'series/' + seriesId);
  }

  getVolumes(seriesId: number) {
    return this.httpClient.get<Volume[]>(this.baseUrl + 'series/volumes?seriesId=' + seriesId);
  }

  getVolume(volumeId: number) {
    return this.httpClient.get<Volume>(this.baseUrl + 'series/volume?volumeId=' + volumeId);
  }

  getChapter(chapterId: number) {
    return this.httpClient.get<Chapter>(this.baseUrl + 'series/chapter?chapterId=' + chapterId);
  }

  getData(id: number) {
    return of(id);
  }

  delete(seriesId: number) {
    return this.httpClient.delete<boolean>(this.baseUrl + 'series/' + seriesId);
  }

  updateRating(seriesId: number, userRating: number, userReview: string) {
    return this.httpClient.post(this.baseUrl + 'series/update-rating', {seriesId, userRating, userReview});
  }
}
