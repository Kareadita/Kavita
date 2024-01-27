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


  getServerInfo() {
    return this.http.get<ServerInfoSlim>(this.baseUrl + 'server/server-info-slim');
  }

  clearCache() {
    return this.http.post(this.baseUrl + 'server/clear-cache', {});
  }

  cleanupWantToRead() {
    return this.http.post(this.baseUrl + 'server/cleanup-want-to-read', {});
  }

  backupDatabase() {
    return this.http.post(this.baseUrl + 'server/backup-db', {});
  }

  analyzeFiles() {
    return this.http.post(this.baseUrl + 'server/analyze-files', {});
  }

  checkForUpdate() {
    return this.http.get<UpdateVersionEvent>(this.baseUrl + 'server/check-update', {});
  }

  checkForUpdates() {
    return this.http.get(this.baseUrl + 'server/check-for-updates', {});
  }

  getChangelog() {
    return this.http.get<UpdateVersionEvent[]>(this.baseUrl + 'server/changelog', {});
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
