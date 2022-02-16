import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ServerInfo } from '../admin/_models/server-info';
import { UpdateVersionEvent } from '../_models/events/update-version-event';

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
    return this.httpClient.get<UpdateVersionEvent>(this.baseUrl + 'server/check-update', {});
  }

  getChangelog() {
    return this.httpClient.get<UpdateVersionEvent[]>(this.baseUrl + 'server/changelog', {});
  }

  isServerAccessible() {
    return this.httpClient.get<boolean>(this.baseUrl + 'server/accessible');
  }
}
