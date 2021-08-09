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

  getServerInfo() {
    return this.httpClient.get<ServerInfo>(this.baseUrl + 'server/server-info');
  }

  clearCache() {
    return this.httpClient.post(this.baseUrl + 'server/clear-cache', {});
  }

  backupDatabase() {
    return this.httpClient.post(this.baseUrl + 'server/backup-db', {});
  }

  checkForUpdate() {
    return this.httpClient.post(this.baseUrl + 'server/check-update', {});
  }
}
