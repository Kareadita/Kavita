import { getLocaleDateFormat } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ServerService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  restart() {
    return this.httpClient.post(this.baseUrl + 'server/restart', {});
  }

  fetchLogs() {
    return this.httpClient.get(this.baseUrl + 'server/logs', {responseType: 'blob' as 'text'});
  }
}
