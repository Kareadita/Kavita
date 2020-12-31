import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { Series } from '../_models/series';

@Injectable({
  providedIn: 'root'
})
export class SeriesService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getSeries(libraryId: number) {
    return this.httpClient.get<Series[]>(this.baseUrl + 'library/series?libraryId=' + libraryId);
  }
}
