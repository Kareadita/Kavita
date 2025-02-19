import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import {ServerInfoSlim} from '../admin/_models/server-info';
import { UpdateVersionEvent } from '../_models/events/update-version-event';
import { Job } from '../_models/job/job';
import { KavitaMediaError } from '../admin/_models/media-error';
import {TextResonse} from "../_types/text-response";
import {map} from "rxjs/operators";

@Injectable({
  providedIn: 'root'
})
export class ServerService {

  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getVersion(apiKey: string) {
    return this.http.get<string>(this.baseUrl + 'plugin/version?apiKey=' + apiKey, TextResonse);
  }

  getServerInfo() {
    return this.http.get<ServerInfoSlim>(this.baseUrl + 'server/server-info-slim');
  }

  clearCache() {
    return this.http.post(this.baseUrl + 'server/clear-cache', {});
  }

  cleanupWantToRead() {
    return this.http.post(this.baseUrl + 'server/cleanup-want-to-read', {});
  }

  cleanup() {
    return this.http.post(this.baseUrl + 'server/cleanup', {});
  }

  backupDatabase() {
    return this.http.post(this.baseUrl + 'server/backup-db', {});
  }

  analyzeFiles() {
    return this.http.post(this.baseUrl + 'server/analyze-files', {});
  }

  syncThemes() {
    return this.http.post(this.baseUrl + 'server/sync-themes', {});
  }

  checkForUpdate() {
    return this.http.get<UpdateVersionEvent | null>(this.baseUrl + 'server/check-update');
  }

  checkHowOutOfDate() {
    return this.http.get<string>(this.baseUrl + 'server/check-out-of-date', TextResonse)
      .pipe(map(r => parseInt(r, 10)));
  }

  checkForUpdates() {
    return this.http.get<UpdateVersionEvent>(this.baseUrl + 'server/check-for-updates', {});
  }

  getChangelog(count: number = 0) {
    return this.http.get<UpdateVersionEvent[]>(this.baseUrl + 'server/changelog?count=' + count, {});
  }

  getRecurringJobs() {
    return this.http.get<Job[]>(this.baseUrl + 'server/jobs');
  }

  convertMedia() {
    return this.http.post(this.baseUrl + 'server/convert-media', {});
  }

  bustCache() {
    return this.http.post(this.baseUrl + 'server/bust-kavitaplus-cache', {});
  }

  getMediaErrors() {
    return this.http.get<Array<KavitaMediaError>>(this.baseUrl + 'server/media-errors', {});
  }

  clearMediaAlerts() {
    return this.http.post(this.baseUrl + 'server/clear-media-alerts', {});
  }
}
