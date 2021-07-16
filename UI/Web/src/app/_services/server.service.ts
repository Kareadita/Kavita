import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ServerInfo } from '../admin/_models/server-info';

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

  getServerInfo() {
    return this.httpClient.get<ServerInfo>(this.baseUrl + 'server/server-info');
  }
}
