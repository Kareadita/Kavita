import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class UploadService {

  private baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }


  /**
   * 
   * @param seriesId Series to overwrite cover image for
   * @param url A base64 encoded url
   * @returns 
   */
  updateSeriesCoverImage(seriesId: number, url: string) {
    if (url.startsWith('data')) {
      url = url.split(',')[1];
    }
    return this.httpClient.post<number>(this.baseUrl + 'upload/series-url', {id: seriesId, url: url});
  }
}
