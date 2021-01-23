import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ServerSettings } from './_models/server-settings';

@Injectable({
  providedIn: 'root'
})
export class SettingsService {

  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getServerSettings() {
    return this.http.get<ServerSettings>(this.baseUrl + 'settings');
  }

  updateServerSettings(model: ServerSettings) {
    return this.http.post<ServerSettings>(this.baseUrl + 'settings', model);
  }
}
